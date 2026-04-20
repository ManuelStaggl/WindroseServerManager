using System;
using System.Collections.Generic;
using System.Linq;

namespace WindroseServerManager.Core.Services;

public static class ReportUrlBuilder
{
    public const string BaseUrl = "https://github.com/HumanGenome/WindrosePlus/issues/new";
    public const int MaxLogLines = 20;
    public const int MaxLogLineChars = 200;
    public const string EmptyLogPlaceholder = "(no server log available)";

    /// <summary>
    /// Builds a prefilled GitHub issue URL for WindrosePlus compatibility reports.
    /// Title and body are encoded with Uri.EscapeDataString.
    /// Log tail lines are capped at MaxLogLines (20) and truncated to MaxLogLineChars (200 chars) each.
    /// Null or empty logTailLines emits EmptyLogPlaceholder in the body.
    /// </summary>
    public static string Build(
        string windroseVersion,
        string windrosePlusVersion,
        int dashboardPort,
        IReadOnlyList<string>? logTailLines)
    {
        var wv  = string.IsNullOrWhiteSpace(windroseVersion)      ? "unknown" : windroseVersion;
        var wpv = string.IsNullOrWhiteSpace(windrosePlusVersion)   ? "unknown" : windrosePlusVersion;

        var title = $"[Compat] WindrosePlus not responding — Windrose {wv} / WP {wpv}";

        string logBlock;
        if (logTailLines is null || logTailLines.Count == 0)
        {
            logBlock = EmptyLogPlaceholder;
        }
        else
        {
            var trimmed = logTailLines
                .Skip(Math.Max(0, logTailLines.Count - MaxLogLines))
                .Select(l => l.Length > MaxLogLineChars ? l[..MaxLogLineChars] : l);
            logBlock = string.Join("\n", trimmed);
        }

        var body =
            "## Environment\n" +
            $"- Windrose Version: `{wv}`\n" +
            $"- WindrosePlus Version: `{wpv}`\n" +
            $"- Dashboard Port: `{dashboardPort}`\n" +
            "- Reported by: Windrose Server Manager\n\n" +
            "## Server Log Tail (last 20 lines)\n" +
            "```\n" +
            logBlock + "\n" +
            "```\n\n" +
            "## Steps to Reproduce\n" +
            "1. Start server with WindrosePlus active\n" +
            "2. WindrosePlus HTTP dashboard does not respond\n\n" +
            "## Expected Behavior\n" +
            $"Dashboard responds on http://localhost:{dashboardPort}/api/status\n";

        return BaseUrl
            + "?title=" + Uri.EscapeDataString(title)
            + "&body="  + Uri.EscapeDataString(body);
    }
}
