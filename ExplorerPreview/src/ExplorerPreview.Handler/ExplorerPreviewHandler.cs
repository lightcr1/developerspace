using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ExplorerPreview.Cache;
using ExplorerPreview.Native;
using ExplorerPreview.Rendering;
using Microsoft.Web.WebView2.Core;

namespace ExplorerPreview.Handler;

/// <summary>
/// Echter Windows-Shell-Preview-Handler: wird von Explorers eingebauter
/// Vorschauleiste (Ansicht -&gt; Vorschaufenster) geladen und rendert direkt
/// in den von Explorer bereitgestellten Fensterbereich - kein Extra-Fenster.
///
/// Läuft als Out-of-Process-COM-Server (via prevhost.exe-Surrogat, siehe
/// Registrierungsskript) im STA-Apartment. DoPreview() muss laut COM-Vertrag
/// synchron zurückkehren; das eigentliche Laden/Rendern läuft bewusst
/// asynchron im Hintergrund weiter (Standard-Praxis bei Preview-Handlern mit
/// potenziell langsamen Quellen wie NAS-Dateien - sonst würde ausgerechnet
/// der Prozess hängen, der laut Auftrag "immer" Vorschau bieten soll).
/// </summary>
[ComVisible(true)]
[Guid(ClsidString)]
[ClassInterface(ClassInterfaceType.None)]
[ProgId("ExplorerPreview.Handler")]
public sealed class ExplorerPreviewHandler :
    IPreviewHandler, IOleWindow, IObjectWithSite, IInitializeWithFile, IDisposable
{
    /// <summary>Feste CLSID dieses Handlers - muss mit den Registrierungsskripten übereinstimmen.</summary>
    public const string ClsidString = "E3A2C7D4-9F1B-4C8E-8A2D-6B1F4E7C9A02";

    private static readonly PreviewCache Cache = new();
    private static readonly RendererSelector Renderer = new();

    private IntPtr _parentHwnd;
    private NativeMethods.RECT _rect;
    private string? _filePath;
    private CoreWebView2Environment? _environment;
    private CoreWebView2Controller? _controller;
    private object? _site;
    private CancellationTokenSource? _previewCts;

    public void Initialize(string pszFilePath, uint grfMode)
    {
        _filePath = pszFilePath;
    }

    public void SetWindow(IntPtr hwnd, ref NativeMethods.RECT rect)
    {
        _parentHwnd = hwnd;
        _rect = rect;
        if (_controller is not null)
        {
            _controller.ParentWindow = hwnd;
            _controller.Bounds = ToRectangle(rect);
        }
    }

    public void SetRect(ref NativeMethods.RECT rect)
    {
        _rect = rect;
        if (_controller is not null)
        {
            _controller.Bounds = ToRectangle(rect);
        }
    }

    public void DoPreview()
    {
        _previewCts?.Cancel();
        var cts = new CancellationTokenSource();
        _previewCts = cts;

        // Fire-and-forget ist hier bewusst: SetWindow/SetRect/DoPreview müssen laut
        // IPreviewHandler-Vertrag synchron zurückkehren. Fehler werden intern
        // gefangen (siehe RunPreviewAsync) und landen als Fehlertext in der Vorschau
        // statt als unbeobachtete Exception den Surrogate-Prozess zu gefährden.
        _ = RunPreviewAsync(cts.Token);
    }

    private async Task RunPreviewAsync(CancellationToken ct)
    {
        try
        {
            if (_filePath is null || _parentHwnd == IntPtr.Zero)
            {
                return;
            }

            _environment ??= await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(AppSettings.CacheDirectory, "webview2-handler"));

            if (ct.IsCancellationRequested)
            {
                return;
            }

            _controller ??= await _environment.CreateCoreWebView2ControllerAsync(_parentHwnd);
            _controller.Bounds = ToRectangle(_rect);
            _controller.IsVisible = true;

            var copyResult = await Cache.GetLocalCopyAsync(_filePath, ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (!copyResult.Success)
            {
                _controller.CoreWebView2.NavigateToString(
                    $"<html><body style='font-family:Segoe UI;color:#888;padding:16px'>Vorschau nicht verfügbar: {copyResult.FailureReason}</body></html>");
                return;
            }

            var content = await Renderer.BuildAsync(copyResult.LocalPath!, ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (content.Kind == PreviewContentKind.NavigateToFile)
            {
                _controller.CoreWebView2.Navigate(new Uri(content.FilePath!).AbsoluteUri);
            }
            else
            {
                _controller.CoreWebView2.NavigateToString(content.Html!);
            }
        }
        catch (OperationCanceledException)
        {
            // Nächste Datei wurde schon ausgewählt, dieser Preview-Request ist obsolet.
        }
        catch (Exception ex)
        {
            try
            {
                _controller?.CoreWebView2.NavigateToString(
                    $"<html><body style='font-family:Segoe UI;color:#c33;padding:16px'>Fehler: {ex.Message}</body></html>");
            }
            catch { /* Controller evtl. schon weg - dann eben keine Fehleranzeige. */ }
        }
    }

    public void Unload()
    {
        _previewCts?.Cancel();
        _controller?.Close();
        _controller = null;
        _filePath = null;
    }

    public void SetFocus()
    {
        _controller?.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);
    }

    public void QueryFocus(out IntPtr phwnd)
    {
        phwnd = _parentHwnd;
    }

    public int TranslateAccelerator(ref MSG pmsg)
    {
        const int S_FALSE = 1; // "nicht behandelt" - Host soll die Tastatureingabe selbst verarbeiten.
        return S_FALSE;
    }

    public void GetWindow(out IntPtr phwnd) => phwnd = _parentHwnd;

    public void ContextSensitiveHelp(bool fEnterMode) { /* nicht unterstützt */ }

    public void SetSite(object? pUnkSite) => _site = pUnkSite;

    public void GetSite(ref Guid riid, out IntPtr ppvSite)
    {
        ppvSite = IntPtr.Zero;
        if (_site is null)
        {
            return;
        }
        IntPtr unknown = Marshal.GetIUnknownForObject(_site);
        try
        {
            Marshal.QueryInterface(unknown, ref riid, out ppvSite);
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    private static Rectangle ToRectangle(NativeMethods.RECT rect) =>
        new(rect.Left, rect.Top, rect.Width, rect.Height);

    public void Dispose()
    {
        _previewCts?.Cancel();
        _controller?.Close();
    }
}
