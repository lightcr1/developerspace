namespace ExplorerPreview.Rendering;

/// <summary>
/// Entscheidet anhand der Dateiendung, wie eine (bereits lokal im Cache liegende)
/// Datei angezeigt wird. Bewusst eigene Renderer statt Windows-IPreviewHandler:
/// dadurch greift die MOTW-"Diese Datei könnte Ihren Computer beschädigen"-Warnung
/// gar nicht erst, weil wir nie den Trust-Check der jeweiligen Office/Shell-Handler
/// aufrufen - wir parsen/rendern die Inhalte selbst bzw. lassen sie von einem
/// isolierten Konverter (LibreOffice) in ein ungefährliches Format (PDF) wandeln,
/// ohne Makros auszuführen.
/// </summary>
public sealed class RendererSelector
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".svg" };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".md", ".log", ".json", ".xml", ".yml", ".yaml", ".ini", ".cfg", ".csv",
          ".cs", ".c", ".cpp", ".h", ".py", ".js", ".ts", ".java", ".ps1", ".sh", ".sql", ".html", ".css" };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".webm", ".mov", ".m4v" };

    private static readonly HashSet<string> OfficeExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".odt", ".ods", ".odp" };

    private readonly OfficeConverter _officeConverter = new();

    public async Task<PreviewContent> BuildAsync(string localPath, CancellationToken ct)
    {
        string ext = Path.GetExtension(localPath);

        if (ImageExtensions.Contains(ext))
        {
            return PreviewContent.ForFile(localPath);
        }

        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            // WebView2 (Edge) bringt einen vollwertigen PDF-Renderer mit - kein
            // separater Reader nötig, und Macros/JS im PDF laufen nicht aktiv aus,
            // wenn man nicht explizit damit interagiert.
            return PreviewContent.ForFile(localPath);
        }

        if (VideoExtensions.Contains(ext))
        {
            return PreviewContent.ForHtml(BuildVideoHtml(localPath));
        }

        if (TextExtensions.Contains(ext))
        {
            return await BuildTextPreviewAsync(localPath, ct);
        }

        if (OfficeExtensions.Contains(ext))
        {
            return await BuildOfficePreviewAsync(localPath, ct);
        }

        return PreviewContent.ForHtml(HtmlHelpers.Message(
            $"Keine Vorschau verfügbar für Dateityp \"{ext}\"."));
    }

    private static async Task<PreviewContent> BuildTextPreviewAsync(string localPath, CancellationToken ct)
    {
        const int maxChars = 200_000; // Riesige Logfiles nicht komplett ins DOM pumpen.
        try
        {
            await using var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var buffer = new char[maxChars];
            int read = await reader.ReadBlockAsync(buffer, ct);
            string text = new string(buffer, 0, read);
            bool truncated = !reader.EndOfStream;
            string html = HtmlHelpers.TextPage(HtmlHelpers.Escape(text) + (truncated ? "\n\n[... gekürzt ...]" : ""));
            return PreviewContent.ForHtml(html);
        }
        catch (Exception ex)
        {
            return PreviewContent.ForHtml(HtmlHelpers.Message($"Textvorschau fehlgeschlagen: {ex.Message}"));
        }
    }

    private async Task<PreviewContent> BuildOfficePreviewAsync(string localPath, CancellationToken ct)
    {
        if (!AppSettings.EnableOfficePreview)
        {
            return PreviewContent.ForHtml(HtmlHelpers.Message(
                "Office-Vorschau ist deaktiviert (AppSettings.EnableOfficePreview)."));
        }

        var result = await _officeConverter.ConvertToPdfAsync(localPath, ct);
        if (!result.Success)
        {
            return PreviewContent.ForHtml(HtmlHelpers.Message(
                $"Office-Vorschau nicht möglich: {result.FailureReason}"));
        }

        return PreviewContent.ForFile(result.PdfPath!);
    }

    private static string BuildVideoHtml(string localPath)
    {
        string uri = new Uri(localPath).AbsoluteUri;
        return $$"""
            <html><head><style>html,body{margin:0;height:100%;background:#000;}
            video{width:100%;height:100%;object-fit:contain;}</style></head>
            <body><video src="{{uri}}" controls muted autoplay></video></body></html>
            """;
    }
}
