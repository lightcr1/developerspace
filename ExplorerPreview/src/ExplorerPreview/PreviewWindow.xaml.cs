using System.Windows;
using ExplorerPreview.Native;
using ExplorerPreview.Rendering;

namespace ExplorerPreview;

public partial class PreviewWindow : Window
{
    private bool _webViewReady;

    public PreviewWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await EnsureWebViewInitializedAsync();
    }

    private async Task EnsureWebViewInitializedAsync()
    {
        if (_webViewReady)
        {
            return;
        }

        try
        {
            await Browser.EnsureCoreWebView2Async();
            _webViewReady = true;
        }
        catch (Exception ex)
        {
            PlaceholderText.Text = $"WebView2 Runtime nicht verfügbar: {ex.Message}\n\n" +
                "Bitte 'Microsoft Edge WebView2 Runtime' installieren.";
            PlaceholderText.Visibility = Visibility.Visible;
        }
    }

    public async Task ShowContentAsync(PreviewContent content)
    {
        await EnsureWebViewInitializedAsync();
        if (!_webViewReady)
        {
            return;
        }

        PlaceholderText.Visibility = Visibility.Collapsed;

        if (content.Kind == PreviewContentKind.NavigateToFile)
        {
            Browser.CoreWebView2.Navigate(new Uri(content.FilePath!).AbsoluteUri);
        }
        else
        {
            Browser.CoreWebView2.NavigateToString(content.Html!);
        }
    }

    public void ShowPlaceholder(string text)
    {
        PlaceholderText.Text = text;
        PlaceholderText.Visibility = Visibility.Visible;
        if (_webViewReady)
        {
            Browser.CoreWebView2.NavigateToString("<html><body style='background:#1e1e1e'></body></html>");
        }
    }

    /// <summary>
    /// Positioniert das Vorschaufenster rechts neben dem angegebenen Explorer-Fenster,
    /// analog zu einem angedockten Panel - ohne tatsächlich in explorer.exe injizieren
    /// zu müssen (das wäre nicht unterstützt und instabil, siehe Projekt-README).
    /// </summary>
    public void DockBeside(IntPtr explorerHwnd)
    {
        if (!NativeMethods.GetWindowRect(explorerHwnd, out var rect))
        {
            return;
        }

        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int width = (int)Width;
        int height = rect.Height;

        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero,
            rect.Right, rect.Top, width, height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER);
    }
}
