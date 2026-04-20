using System;
using System.Collections.Generic;
using System.Linq;
using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests.Phase10;

public class ReportUrlBuilderTests
{
    private static string DecodeBody(string url)
    {
        var q = new Uri(url).Query.TrimStart('?');
        var bodyParam = q.Split('&').First(p => p.StartsWith("body="))[5..];
        return Uri.UnescapeDataString(bodyParam);
    }

    [Fact]
    public void Build_WithLogTail_ContainsEscapedVersionsAndBody()
    {
        const string windroseVer = "0.10.0-CL12345";
        const string wplusVer = "v1.2.3";
        const int port = 7777;
        var logTail = new List<string> { "line1", "line2" };

        var url = ReportUrlBuilder.Build(windroseVer, wplusVer, port, logTail);

        Assert.StartsWith("https://github.com/HumanGenome/WindrosePlus/issues/new?title=", url);
        Assert.Contains("&body=", url);

        var body = DecodeBody(url);
        Assert.Contains("0.10.0-CL12345", body);
        Assert.Contains("v1.2.3", body);
        Assert.Contains("7777", body);
        Assert.Contains("line1", body);
        Assert.Contains("line2", body);
    }

    [Fact]
    public void Build_WithEmptyLog_UsesPlaceholder()
    {
        var url = ReportUrlBuilder.Build("0.10.0", "v1.0.0", 8080, null);
        var body = DecodeBody(url);

        Assert.Contains("(no server log available)", body);
    }

    [Fact]
    public void Build_TruncatesLongLines_And_CapsAt20Lines()
    {
        // 30 lines each of 500 chars
        var logTail = Enumerable.Range(1, 30)
            .Select(i => new string('x', 500))
            .ToList();

        var url = ReportUrlBuilder.Build("0.10.0", "v1.0.0", 7777, logTail);
        var body = DecodeBody(url);

        // Extract the log block (between the ``` fences)
        var fenceStart = body.IndexOf("```\n", StringComparison.Ordinal) + 4;
        var fenceEnd = body.IndexOf("\n```", fenceStart, StringComparison.Ordinal);
        var logBlock = body[fenceStart..fenceEnd];

        var logLines = logBlock
            .Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        Assert.Equal(20, logLines.Length);
        Assert.All(logLines, line => Assert.True(line.Length <= 200,
            $"Line exceeds 200 chars: length={line.Length}"));
    }
}
