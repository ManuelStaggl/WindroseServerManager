using System.Net;
using System.Net.Http;
using System.Text;
using WindroseServerManager.Core.Tests.TestDoubles;

namespace WindroseServerManager.Core.Tests.Fixtures;

public sealed class FakeGithubReleaseServer
{
    public string WindrosePlusApiUrl { get; } = "https://api.github.com/repos/HumanGenome/WindrosePlus/releases/latest";
    public string Ue4ssApiUrl { get; } = "https://api.github.com/repos/UE4SS-RE/RE-UE4SS/releases/latest";
    public string WindrosePlusAssetUrl { get; } = "https://github.com/HumanGenome/WindrosePlus/releases/download/v1.0.6/WindrosePlus.zip";
    public string Ue4ssAssetUrl { get; } = "https://github.com/UE4SS-RE/RE-UE4SS/releases/download/experimental-latest/UE4SS_v3.0.1.zip";

    public byte[] WindrosePlusArchive { get; }
    public byte[] Ue4ssArchive { get; }
    public string WindrosePlusSha256 { get; }
    public string Ue4ssSha256 { get; }
    public string WindrosePlusTag { get; set; } = "v1.0.6";
    public string Ue4ssTag { get; set; } = "experimental-latest";

    /// <summary>When false, the asset "digest" field is serialized as null (simulates pre-June-2025 GitHub releases).</summary>
    public bool PublishDigest { get; set; } = true;

    /// <summary>When true, the WindrosePlus archive bytes are mutated in transit so downloaded SHA-256 != published digest.</summary>
    public bool TamperArchive { get; set; } = false;

    /// <summary>When true, the WindrosePlus asset endpoint returns HTTP 500 (simulates mid-download server failure).</summary>
    public bool FailWindrosePlusAsset { get; set; } = false;

    public FakeGithubReleaseServer()
    {
        var (wBytes, wSha) = SampleArchiveBuilder.BuildWindrosePlusZip();
        var (uBytes, uSha) = SampleArchiveBuilder.BuildUe4ssZip();
        WindrosePlusArchive = wBytes; WindrosePlusSha256 = wSha;
        Ue4ssArchive = uBytes; Ue4ssSha256 = uSha;
    }

    public FakeHttpMessageHandler CreateHandler() => new FakeHttpMessageHandler(Handle);

    private Task<HttpResponseMessage> Handle(HttpRequestMessage req, CancellationToken ct)
    {
        var url = req.RequestUri!.ToString();
        if (url == WindrosePlusApiUrl)
            return Json(BuildReleaseJson(WindrosePlusTag, "WindrosePlus.zip", WindrosePlusAssetUrl, WindrosePlusArchive.Length, PublishDigest ? WindrosePlusSha256 : null));
        if (url == Ue4ssApiUrl)
            return Json(BuildReleaseJson(Ue4ssTag, "UE4SS_v3.0.1.zip", Ue4ssAssetUrl, Ue4ssArchive.Length, PublishDigest ? Ue4ssSha256 : null));
        if (url == WindrosePlusAssetUrl)
        {
            if (FailWindrosePlusAsset)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            return Bytes(TamperArchive ? MutateFirstByte(WindrosePlusArchive) : WindrosePlusArchive);
        }
        if (url == Ue4ssAssetUrl) return Bytes(Ue4ssArchive);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static string BuildReleaseJson(string tag, string assetName, string url, int size, string? shaHex)
    {
        var digestField = shaHex is null ? "null" : $"\"sha256:{shaHex.ToLowerInvariant()}\"";
        return $$"""
        {
          "tag_name": "{{tag}}",
          "draft": false,
          "prerelease": false,
          "assets": [
            { "name": "{{assetName}}", "browser_download_url": "{{url}}", "size": {{size}}, "digest": {{digestField}} }
          ]
        }
        """;
    }

    private static Task<HttpResponseMessage> Json(string body) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    private static Task<HttpResponseMessage> Bytes(byte[] bytes) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });

    private static byte[] MutateFirstByte(byte[] src)
    {
        var c = (byte[])src.Clone();
        c[0] = (byte)(c[0] ^ 0xFF);
        return c;
    }
}
