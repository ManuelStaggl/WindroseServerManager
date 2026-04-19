using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace WindroseServerManager.App.Services;

/// <summary>
/// Mapped Exceptions auf benutzerfreundliche deutsche Fehlermeldungen.
/// </summary>
public static class ErrorMessageHelper
{
    public static string FriendlyMessage(Exception ex) => ex switch
    {
        UnauthorizedAccessException => Loc.Get("Error.AccessDenied"),
        DirectoryNotFoundException dnf => string.IsNullOrWhiteSpace(dnf.Message)
            ? Loc.Get("Error.DirectoryNotFound")
            : Loc.Format("Error.DirectoryNotFoundFormat", dnf.Message),
        FileNotFoundException fnf => string.IsNullOrWhiteSpace(fnf.FileName)
            ? Loc.Get("Error.FileNotFound")
            : Loc.Format("Error.FileNotFoundFormat", fnf.FileName),
        JsonException => Loc.Get("Error.ConfigCorrupt"),
        HttpRequestException => Loc.Get("Error.NoInternet"),
        OperationCanceledException => Loc.Get("Error.Canceled"),
        IOException io => Loc.Format("Error.IoErrorFormat", io.Message),
        _ => ex.Message,
    };
}
