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
        UnauthorizedAccessException => "Zugriff verweigert. Bitte App als Administrator ausführen.",
        DirectoryNotFoundException dnf => string.IsNullOrWhiteSpace(dnf.Message)
            ? "Ordner nicht gefunden."
            : $"Ordner nicht gefunden: {dnf.Message}",
        FileNotFoundException fnf => string.IsNullOrWhiteSpace(fnf.FileName)
            ? "Datei nicht gefunden."
            : $"Datei nicht gefunden: {fnf.FileName}",
        JsonException => "Konfigurationsdatei ist beschädigt. Bitte Server neu starten, damit sie regeneriert wird.",
        HttpRequestException => "Keine Internet-Verbindung.",
        OperationCanceledException => "Vorgang abgebrochen.",
        IOException io => $"Datei-Zugriffsfehler: {io.Message}",
        _ => ex.Message,
    };
}
