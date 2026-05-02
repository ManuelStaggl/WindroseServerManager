using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.Services;

/// <summary>
/// Discord bot background service that manages a Discord bot lifecycle, handles slash commands,
/// updates bot activity based on server status, and streams server logs to a configured channel.
/// </summary>
public sealed class DiscordBotService : BackgroundService, IAsyncDisposable
{
    private readonly ILogger<DiscordBotService> _logger;
    private readonly IAppSettingsService _settings;
    private readonly IServerProcessService _serverProcess;
    private readonly IMetricsService _metrics;
    private readonly IServerEventLog _eventLog;
    private readonly IServiceProvider _serviceProvider;

    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactionService;
    private CancellationTokenSource? _cts;
    
    // Log buffering state
    private readonly ConcurrentQueue<string> _logBuffer = new();
    private DateTime _lastLogFlushUtc = DateTime.UtcNow;
    private DateTime _lastPresenceUpdateUtc = DateTime.MinValue;
    private const int LogFlushIntervalSeconds = 3;
    private const int LogMessageMaxCharacters = 1900;

    public DiscordBotService(
        ILogger<DiscordBotService> logger,
        IAppSettingsService settings,
        IServerProcessService serverProcess,
        IMetricsService metrics,
        IServerEventLog eventLog,
        IServiceProvider serviceProvider,
        DiscordSocketClient client,
        InteractionService interactionService)
    {
        _logger = logger;
        _settings = settings;
        _serverProcess = serverProcess;
        _metrics = metrics;
        _eventLog = eventLog;
        _serviceProvider = serviceProvider;
        _client = client;
        _interactionService = interactionService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        var settings = _settings.Current;
        if (!settings.EnableDiscordBot || string.IsNullOrWhiteSpace(settings.DiscordBotToken))
        {
            _logger.LogInformation("Discord bot is disabled");
            return;
        }

        if (settings.DiscordGuildId == 0)
        {
            _logger.LogWarning("Discord bot enabled but DiscordGuildId is not set");
            return;
        }

        try
        {
            _logger.LogInformation("Starting Discord bot service...");

            // Wire up event handlers
            _client.Log += OnClientLog;
            _client.Ready += OnClientReady;
            _client.Disconnected += OnClientDisconnected;

            // Register slash commands
            _interactionService.Log += OnInteractionServiceLog;
            _client.InteractionCreated += OnInteractionCreated;

            // Subscribe to server events instead of raw logs
            _serverProcess.StatusChanged += OnServerStatusChanged;
            _eventLog.Appended += OnServerEventAppended;

            // Connect bot
            await _client.LoginAsync(TokenType.Bot, settings.DiscordBotToken).ConfigureAwait(false);
            await _client.StartAsync().ConfigureAwait(false);

            _logger.LogInformation("Discord bot connected");

            // Log flush loop
            _ = LogFlushLoopAsync(_cts.Token);

            // Keep the service running
            await Task.Delay(Timeout.Infinite, _cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Discord bot service cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discord bot service encountered an error");
        }
    }

    private async Task OnClientReady()
    {
        try
        {
            _logger.LogInformation("Discord bot ready. Registering slash commands to guild {GuildId}", _settings.Current.DiscordGuildId);

            // Register slash command modules with proper DI service provider
            var modules = await _interactionService.AddModulesAsync(typeof(DiscordBotService).Assembly, _serviceProvider)
                .ConfigureAwait(false);
            _logger.LogInformation("Loaded {ModuleCount} interaction modules", modules.Count());

            // Register to guild (dev/test) or globally
            var commandsRegistered = await _interactionService.RegisterCommandsToGuildAsync(_settings.Current.DiscordGuildId)
                .ConfigureAwait(false);
            _logger.LogInformation("Registered {CommandCount} slash commands to guild {GuildId}", 
                commandsRegistered.Count, _settings.Current.DiscordGuildId);

            foreach (var cmd in commandsRegistered)
            {
                _logger.LogDebug("Registered command: {CommandName}", cmd.Name);
            }

            if (commandsRegistered.Count == 0)
            {
                _logger.LogWarning("No slash commands were registered! Check if any modules contain SlashCommand attributes");
            }

            // Update activity to current server status
            await UpdateBotActivityAsync().ConfigureAwait(false);
            _logger.LogInformation("Bot activity updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bot ready initialization - check logs above for details");
            throw;
        }
    }

    private async Task OnClientDisconnected(Exception arg)
    {
        _logger.LogWarning("Discord bot disconnected: {Message}", arg?.Message);
        // Reconnection is handled automatically by Discord.Net
    }

    private Task OnClientLog(LogMessage msg)
    {
        var level = msg.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Information,
        };

        _logger.Log(level, msg.Exception, "[Discord.Net] {Source}: {Message}", msg.Source, msg.Message);
        return Task.CompletedTask;
    }

    private Task OnInteractionServiceLog(LogMessage msg)
    {
        var level = msg.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Information,
        };

        _logger.Log(level, msg.Exception, "[Discord.Net.Interactions] {Source}: {Message}", msg.Source, msg.Message);
        return Task.CompletedTask;
    }

    private async Task OnInteractionCreated(SocketInteraction interaction)
    {
        try
        {
            var ctx = new SocketInteractionContext(_client, interaction);
            var result = await _interactionService.ExecuteCommandAsync(ctx, _serviceProvider).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Interaction execution failed: {Error}", result.ErrorReason);

                // Respond with error if possible
                if (!interaction.HasResponded)
                {
                    await interaction.RespondAsync(Loc.Format("Discord.Command.Error.General", result.ErrorReason), ephemeral: true)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling interaction");
            try
            {
                if (!interaction.HasResponded)
                {
                    await interaction.RespondAsync(Loc.Get("Discord.Command.Error.Internal"), ephemeral: true).ConfigureAwait(false);
                }
            }
            catch { /* ignore */ }
        }
    }

    private void OnServerStatusChanged(ServerStatus status)
    {
        try
        {
            _ = UpdateBotActivityAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update bot activity on status change");
        }
    }

    private void OnServerEventAppended(ServerEvent evt)
    {
        var emoji = evt.Type switch
        {
            ServerEventType.Started => "🟢",
            ServerEventType.Stopped => "🔴",
            ServerEventType.Crashed => "⚠️",
            ServerEventType.ScheduledRestart => "🔄",
            ServerEventType.AutoRestartHighRam => "🔄",
            ServerEventType.AutoRestartMaxUptime => "🔄",
            ServerEventType.BackupOnRestartSuccess or ServerEventType.BackupAutomatic or ServerEventType.BackupManual => "✅",
            ServerEventType.BackupOnRestartFailed => "❌",
            ServerEventType.BackupRestored => "⏪",
            ServerEventType.BackupDeleted => "🗑️",
            _ => "ℹ️",
        };

        var eventName = evt.Type switch
        {
            ServerEventType.Started => Loc.Get("Event.Started"),
            ServerEventType.Stopped => Loc.Get("Event.Stopped"),
            ServerEventType.Crashed => Loc.Get("Event.Crashed"),
            ServerEventType.ScheduledRestart => Loc.Get("Event.ScheduledRestart"),
            ServerEventType.AutoRestartHighRam => Loc.Get("Event.AutoRestartRam"),
            ServerEventType.AutoRestartMaxUptime => Loc.Get("Event.AutoRestartUptime"),
            ServerEventType.BackupOnRestartSuccess => Loc.Get("Event.BackupOnRestartSuccess"),
            ServerEventType.BackupOnRestartFailed => Loc.Get("Event.BackupOnRestartFailed"),
            ServerEventType.BackupManual => Loc.Get("Event.BackupManual"),
            ServerEventType.BackupAutomatic => Loc.Get("Event.BackupAutomatic"),
            ServerEventType.BackupRestored => Loc.Get("Event.BackupRestored"),
            ServerEventType.BackupDeleted => Loc.Get("Event.BackupDeleted"),
            _ => evt.Type.ToString(),
        };

        var time = evt.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
        var durationStr = "";
        if (evt.SessionDuration is { } d && d.TotalSeconds > 0)
        {
            var durText = d.TotalHours >= 1
                ? Loc.Format("Event.SessionDurationHm", (int)d.TotalHours, d.Minutes)
                : Loc.Format("Event.SessionDurationMs", d.Minutes, d.Seconds);
            durationStr = $" *({durText})*";
        }
        
        var serverLabel = !string.IsNullOrWhiteSpace(evt.ServerName) ? $"**[{evt.ServerName}]** " : "";
        _logBuffer.Enqueue(Loc.Format("Discord.Event.LogFormat", time, emoji, serverLabel, eventName, evt.Reason, durationStr));
    }

    private async Task<bool> UpdateBotActivityAsync()
    {
        if (_client.ConnectionState != ConnectionState.Connected)
        {
            _logger.LogWarning("Cannot update bot activity: client is null");
            return false;
        }

        try
        {
            var status = _serverProcess.Status;
            var uptime = _serverProcess.StartedAtUtc is { } startedAt
                ? DateTime.UtcNow - startedAt
                : TimeSpan.Zero;

            var activity = status switch
            {
                ServerStatus.Running => new Game(
                    Loc.Format("Discord.Status.RunningFormat", (int)uptime.TotalHours, uptime.Minutes),
                    ActivityType.Playing),
                ServerStatus.Starting => new Game(Loc.Get("Discord.Status.Starting"), ActivityType.Playing),
                ServerStatus.Stopping => new Game(Loc.Get("Discord.Status.Stopping"), ActivityType.Playing),
                ServerStatus.Stopped => new Game(Loc.Get("Discord.Status.Stopped"), ActivityType.Playing),
                _ => new Game(Loc.Get("Discord.Status.Unknown"), ActivityType.Playing),
            };

            await _client.SetActivityAsync(activity).ConfigureAwait(false);
            _lastPresenceUpdateUtc = DateTime.UtcNow;
            _logger.LogDebug("Bot activity updated to: {Activity}", activity.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "❌ Failed to update bot activity");
            return false;
        }
    }

    private async Task LogFlushLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(LogFlushIntervalSeconds), ct).ConfigureAwait(false);

                // Update presence/uptime periodically (every 1 minute)
                if ((DateTime.UtcNow - _lastPresenceUpdateUtc).TotalMinutes >= 1)
                {
                    await UpdateBotActivityAsync().ConfigureAwait(false);
                }

                if (_logBuffer.IsEmpty) continue;

                var settings = _settings.Current;
                if (settings.DiscordLogChannelId == 0) continue;

                try
                {
                    var channel = _client.GetChannel(settings.DiscordLogChannelId) as ITextChannel;
                    if (channel == null)
                    {
                        _logger.LogWarning("Discord log channel {ChannelId} not found or not a text channel", settings.DiscordLogChannelId);
                        continue;
                    }

                    var sb = new StringBuilder();

                    while (_logBuffer.TryDequeue(out var logLine))
                    {
                        if (sb.Length + logLine.Length + 5 > LogMessageMaxCharacters)
                        {
                            // Send current batch and start a new one
                            await SendLogMessageAsync(channel, sb.ToString()).ConfigureAwait(false);
                            sb.Clear();
                            sb.AppendLine(logLine);
                        }
                        else
                        {
                            sb.AppendLine(logLine);
                        }
                    }

                    // Send remaining logs
                    if (sb.Length > 0)
                    {
                        await SendLogMessageAsync(channel, sb.ToString()).ConfigureAwait(false);
                    }

                    _lastLogFlushUtc = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to flush logs to Discord");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Log flush loop encountered an error");
        }
    }

    private async Task SendLogMessageAsync(ITextChannel channel, string message)
    {
        try
        {
            await channel.SendMessageAsync(message).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429"))
        {
            // Rate limited, wait a bit before retrying
            _logger.LogWarning("Discord rate limit hit, waiting before retry");
            await Task.Delay(1000).ConfigureAwait(false);
            await channel.SendMessageAsync(message).ConfigureAwait(false);
        }
        catch (Discord.Net.HttpException ex) when ((int)ex.DiscordCode == 50001 || ex.DiscordCode == DiscordErrorCode.MissingPermissions)
        {
            _logger.LogWarning("Discord bot Missing Access (50001) or Permissions for channel {ChannelId}. Check Bot permissions in Discord.", channel.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send log message to Discord channel");
        }
    }

    private async Task FlushRemainingLogsAsync()
    {
        if (_logBuffer.IsEmpty) return;

        try
        {
            var settings = _settings.Current;
            var channel = _client.GetChannel(settings.DiscordLogChannelId) as ITextChannel;
            if (channel == null) return;

            var sb = new StringBuilder();
            while (_logBuffer.TryDequeue(out var logLine))
            {
                    if (sb.Length + logLine.Length + 5 > LogMessageMaxCharacters)
                {
                    await SendLogMessageAsync(channel, sb.ToString()).ConfigureAwait(false);
                    sb.Clear();
                    sb.AppendLine(logLine);
                }
                else
                {
                    sb.AppendLine(logLine);
                }
            }

                if (sb.Length > 0)
            {
                await SendLogMessageAsync(channel, sb.ToString()).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to flush remaining logs on shutdown");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Discord bot service...");

        // Unsubscribe from events
        if (_serverProcess != null)
        {
            _serverProcess.StatusChanged -= OnServerStatusChanged;
        }
        if (_eventLog != null)
        {
            _eventLog.Appended -= OnServerEventAppended;
        }

        // Flush any remaining logs
        await FlushRemainingLogsAsync().ConfigureAwait(false);

        // Disconnect bot gracefully
        try
        {
            await _client.LogoutAsync().ConfigureAwait(false);
            await _client.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during bot shutdown");
        }

        _cts?.Cancel();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _client.Dispose();
        _interactionService.Dispose();
        _cts?.Dispose();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Slash command module for Discord bot server control.
/// </summary>
[Group("server", "Server management commands")]
public class ServerCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IServerProcessService _serverProcess;
    private readonly IMetricsService _metrics;
    private readonly IBackupService _backupService;
    private readonly ILogger<ServerCommandModule> _logger;

    public ServerCommandModule(
        IServerProcessService serverProcess,
        IMetricsService metrics,
        IBackupService backupService,
        ILogger<ServerCommandModule> logger)
    {
        _serverProcess = serverProcess;
        _metrics = metrics;
        _backupService = backupService;
        _logger = logger;
    }

    [SlashCommand("status", "Displays the current server status")]
    public async Task StatusCommand()
    {
        await DeferAsync().ConfigureAwait(false);

        try
        {
            var status = _serverProcess.Status;
            var metrics = _metrics.GetServerProcessMetrics();
            var uptime = _serverProcess.StartedAtUtc is { } startedAt
                ? DateTime.UtcNow - startedAt
                : TimeSpan.Zero;

            var embed = new EmbedBuilder()
                .WithTitle(Loc.Get("Discord.Command.Status.Title"))
                .WithColor(status == ServerStatus.Running ? Color.Green : Color.Red)
                .AddField(Loc.Get("Discord.Command.Status.State"), GetStatusEmoji(status) + " " + Loc.Get($"ServerStatus.{status}"), true)
                .AddField(Loc.Get("Discord.Command.Status.Uptime"), FormatUptime(uptime), true)
                .AddField(Loc.Get("Discord.Command.Status.Pid"), _serverProcess.ProcessId?.ToString() ?? "N/A", true)
                .WithTimestamp(DateTime.UtcNow)
                .WithFooter("Windrose Server Manager");

            if (metrics != null)
            {
                var ramMb = metrics.RamBytes / (1024 * 1024);
                embed.AddField(Loc.Get("Discord.Command.Status.Ram"), $"{ramMb} MB", true);
                embed.AddField(Loc.Get("Discord.Command.Status.Cpu"), $"{metrics.CpuPercent:F1}%", true);
            }

            await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in status command");
            await FollowupAsync(Loc.Get("Discord.Command.Error.Status")).ConfigureAwait(false);
        }
    }

    [SlashCommand("start", "Starts the server")]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    public async Task StartCommand()
    {
        await DeferAsync().ConfigureAwait(false);

        try
        {
            if (_serverProcess.Status is ServerStatus.Running or ServerStatus.Starting)
            {
                await FollowupAsync(Loc.Get("Discord.Command.Start.AlreadyRunning")).ConfigureAwait(false);
                return;
            }

            _logger.LogInformation("Discord bot: Start command invoked by {User}", Context.User.Username);
            var success = await _serverProcess.StartAsync().ConfigureAwait(false);

            if (success)
            {
                await FollowupAsync(Loc.Get("Discord.Command.Start.Success")).ConfigureAwait(false);
            }
            else
            {
                await FollowupAsync(Loc.Get("Discord.Command.Start.Failed")).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in start command");
            await FollowupAsync(Loc.Get("Discord.Command.Start.Error")).ConfigureAwait(false);
        }
    }

    [SlashCommand("stop", "Stops the server")]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    public async Task StopCommand()
    {
        await DeferAsync().ConfigureAwait(false);

        try
        {
            if (_serverProcess.Status is ServerStatus.Stopped or ServerStatus.Stopping)
            {
                await FollowupAsync(Loc.Get("Discord.Command.Stop.AlreadyStopped")).ConfigureAwait(false);
                return;
            }

            _logger.LogInformation("Discord bot: Stop command invoked by {User}", Context.User.Username);
            await _serverProcess.StopAsync().ConfigureAwait(false);
            await FollowupAsync(Loc.Get("Discord.Command.Stop.Success")).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in stop command");
            await FollowupAsync(Loc.Get("Discord.Command.Stop.Error")).ConfigureAwait(false);
        }
    }

    [SlashCommand("restart", "Restarts the game server")]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    public async Task RestartCommand()
    {
        await DeferAsync().ConfigureAwait(false);

        try
        {
            _logger.LogInformation("Discord bot: Restart command invoked by {User}", Context.User.Username);

            await FollowupAsync(Loc.Get("DiscordBot.Cmd.Restarting")).ConfigureAwait(false);
            await _serverProcess.StopAsync().ConfigureAwait(false);
            await Task.Delay(5000).ConfigureAwait(false);
            var success = await _serverProcess.StartAsync().ConfigureAwait(false);

            if (success)
            {
                await ModifyOriginalResponseAsync(m => m.Content = Loc.Get("DiscordBot.Cmd.RestartDone")).ConfigureAwait(false);
            }
            else
            {
                await ModifyOriginalResponseAsync(m => m.Content = Loc.Get("DiscordBot.Cmd.RestartFailed")).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in restart command");
            await ModifyOriginalResponseAsync(m => m.Content = Loc.Format("DiscordBot.Cmd.Error", ex.Message)).ConfigureAwait(false);
        }
    }

    [SlashCommand("backup", "Creates a manual backup")]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    public async Task BackupCommand()
    {
        await RespondAsync(Loc.Get("DiscordBot.Cmd.BackupInProgress")).ConfigureAwait(false);

        try
        {
            _logger.LogInformation("Discord bot: Backup command invoked by {User}", Context.User.Username);

            var info = await _backupService.CreateBackupAsync(isAutomatic: false).ConfigureAwait(false);

            if (info is not null)
            {
                await ModifyOriginalResponseAsync(m => m.Content = Loc.Format("DiscordBot.Cmd.BackupSuccess", info.FileName)).ConfigureAwait(false);
            }
            else
            {
                await ModifyOriginalResponseAsync(m => m.Content = Loc.Get("DiscordBot.Cmd.BackupMissing")).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in backup command");
            await ModifyOriginalResponseAsync(m => m.Content = Loc.Format("DiscordBot.Cmd.Error", ex.Message)).ConfigureAwait(false);
        }
    }

    [SlashCommand("backuprestart", "Stops server, creates backup, and restarts")]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    public async Task BackupRestartCommand()
    {
        await RespondAsync(Loc.Get("DiscordBot.Cmd.Stopping")).ConfigureAwait(false);

        try
        {
            _logger.LogInformation("Discord bot: BackupRestart command invoked by {User}", Context.User.Username);

            await _serverProcess.StopAsync().ConfigureAwait(false);

            await ModifyOriginalResponseAsync(m => m.Content = Loc.Get("DiscordBot.Cmd.BackupInProgress")).ConfigureAwait(false);
            var info = await _backupService.CreateBackupAsync(isAutomatic: false).ConfigureAwait(false);

            if (info is null)
            {
                await ModifyOriginalResponseAsync(m => m.Content = Loc.Get("DiscordBot.Cmd.BackupFailedRestart")).ConfigureAwait(false);
            }

            await ModifyOriginalResponseAsync(m => m.Content = Loc.Get("DiscordBot.Cmd.Starting")).ConfigureAwait(false);
            var success = await _serverProcess.StartAsync().ConfigureAwait(false);

            if (success)
            {
                await ModifyOriginalResponseAsync(m => m.Content = Loc.Get("DiscordBot.Cmd.Success")).ConfigureAwait(false);
            }
            else
            {
                await ModifyOriginalResponseAsync(m => m.Content = Loc.Get("DiscordBot.Cmd.BackupOkRestartFailed")).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in backuprestart command");
            await ModifyOriginalResponseAsync(m => m.Content = Loc.Format("DiscordBot.Cmd.Error", ex.Message)).ConfigureAwait(false);
        }
    }

    private static string GetStatusEmoji(ServerStatus status) => status switch
    {
        ServerStatus.Running => "🟢",
        ServerStatus.Starting => "🟡",
        ServerStatus.Stopping => "🟠",
        ServerStatus.Stopped => "🔴",
        _ => "⚫",
    };

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalSeconds < 1) return "< 1s";
        if (uptime.TotalMinutes < 1) return $"{uptime.Seconds}s";
        if (uptime.TotalHours < 1) return $"{uptime.Minutes}m {uptime.Seconds}s";
        if (uptime.TotalDays < 1) return $"{uptime.Hours}h {uptime.Minutes}m";
        return $"{uptime.Days}d {uptime.Hours}h";
    }
}
