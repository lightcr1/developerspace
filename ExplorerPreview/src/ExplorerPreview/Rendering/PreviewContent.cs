namespace ExplorerPreview.Rendering;

public enum PreviewContentKind
{
    /// <summary>Direkt eine lokale Datei per file:// in WebView2 laden (Bild, PDF, Video).</summary>
    NavigateToFile,
    /// <summary>HTML-Fragment per NavigateToString anzeigen (Text/Code, Fehlermeldungen, Fallback-Info).</summary>
    Html,
}

public sealed class PreviewContent
{
    public required PreviewContentKind Kind { get; init; }
    public string? FilePath { get; init; }
    public string? Html { get; init; }

    public static PreviewContent ForFile(string path) => new() { Kind = PreviewContentKind.NavigateToFile, FilePath = path };
    public static PreviewContent ForHtml(string html) => new() { Kind = PreviewContentKind.Html, Html = html };
}
