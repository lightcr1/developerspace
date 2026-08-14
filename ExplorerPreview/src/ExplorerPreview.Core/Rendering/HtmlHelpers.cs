using System.Net;

namespace ExplorerPreview.Rendering;

internal static class HtmlHelpers
{
    private const string BaseStyle = """
        <style>
          html, body { margin:0; padding:0; height:100%; background:#1e1e1e; color:#ddd;
            font-family: 'Cascadia Code','Consolas',monospace; }
          .msg { display:flex; align-items:center; justify-content:center; height:100%;
            padding:24px; box-sizing:border-box; font-family: Segoe UI, sans-serif; font-size:14px;
            text-align:center; color:#bbb; }
          pre { margin:0; padding:12px 16px; white-space:pre-wrap; word-break:break-word; font-size:13px; }
        </style>
        """;

    public static string TextPage(string escapedOrRawText, bool preformatted = true)
    {
        string body = preformatted ? $"<pre>{escapedOrRawText}</pre>" : escapedOrRawText;
        return $"<html><head>{BaseStyle}</head><body>{body}</body></html>";
    }

    public static string Message(string text) =>
        $"<html><head>{BaseStyle}</head><body><div class=\"msg\">{Escape(text)}</div></body></html>";

    public static string Escape(string text) => WebUtility.HtmlEncode(text);
}
