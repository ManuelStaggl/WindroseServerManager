using Avalonia;
using Serilog;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WindroseServerManager.App;

sealed class Program
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindroseServerManager");

    private static readonly string CrashDir = Path.Combine(AppDataDir, "crashes");
    private static readonly string LogDir = Path.Combine(AppDataDir, "logs");

    public static string CrashDirectory => CrashDir;

    /// <summary>True wenn die App via --tray oder --minimized gestartet wurde (Autostart-Modus).</summary>
    public static bool StartMinimizedToTray { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        foreach (var a in args)
        {
            if (string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase))
            {
                StartMinimizedToTray = true;
                break;
            }
        }

        try
        {
            Directory.CreateDirectory(CrashDir);
            Directory.CreateDirectory(LogDir);
        }
        catch { }

        ConfigureSerilog();

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Log.Information("Startup...");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // RedirectionSurface = GDI-sichtbare Rendering-Oberfläche. Default wäre
            // WinUIComposition/DirectComposition — GPU-Surfaces, die BitBlt/PrintWindow
            // (Greenshot, ShareX-GDI, klassische Screenshot-APIs) schwarz zurückgeben.
            .With(new Avalonia.Win32PlatformOptions
            {
                CompositionMode = new[] { Avalonia.Win32CompositionMode.RedirectionSurface },
            })
            .WithInterFont()
            .LogToTrace();

    private static void ConfigureSerilog()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(LogDir, "app-.log"),
                rollingInterval: Serilog.RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogCrash(ex);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            LogCrash(e.Exception);
            Log.Error(e.Exception, "Unobserved task exception (marked as observed)");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to log unobserved task exception");
        }
        finally
        {
            e.SetObserved();
        }
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(CrashDir);
            var ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var path = Path.Combine(CrashDir, $"crash-{ts}.txt");

            var sb = new StringBuilder();
            sb.AppendLine($"Timestamp:    {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"OS:           {Environment.OSVersion}");
            sb.AppendLine($".NET:         {Environment.Version}");
            sb.AppendLine($"App-Version:  {Assembly.GetExecutingAssembly().GetName().Version}");
            sb.AppendLine($"64-bit:       {Environment.Is64BitProcess}");
            sb.AppendLine(new string('-', 60));
            sb.AppendLine("Exception Chain:");
            sb.AppendLine();

            var current = ex;
            int depth = 0;
            while (current is not null)
            {
                sb.AppendLine($"[{depth}] {current.GetType().FullName}: {current.Message}");
                sb.AppendLine(current.StackTrace);
                sb.AppendLine();
                current = current.InnerException;
                depth++;
            }

            File.WriteAllText(path, sb.ToString());

            try { Log.Fatal(ex, "Crash persisted to {Path}", path); } catch { }
        }
        catch
        {
            // Crash-Logger darf nie selbst crashen.
        }
    }
}
