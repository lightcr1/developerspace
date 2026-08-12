using System.Windows;
using ExplorerPreview.Cache;
using ExplorerPreview.Rendering;
using ExplorerPreview.Shell;
using Forms = System.Windows.Forms;

namespace ExplorerPreview;

public partial class App : System.Windows.Application
{
    private PreviewWindow? _previewWindow;
    private SelectionWatcher? _watcher;
    private readonly PreviewCache _cache = new();
    private readonly RendererSelector _renderer = new();
    private Forms.NotifyIcon? _trayIcon;

    private CancellationTokenSource? _pendingRequest;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _previewWindow = new PreviewWindow();
        _previewWindow.Show();

        SetupTrayIcon();

        _watcher = new SelectionWatcher();
        _watcher.SelectionChanged += OnSelectionChanged;
        _watcher.ForegroundExplorerWindowChanged += OnForegroundExplorerWindowChanged;
        _watcher.Start();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "Explorer Preview",
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Beenden", null, (_, _) => Shutdown());
        _trayIcon.ContextMenuStrip = menu;
    }

    private async void OnSelectionChanged(string? path)
    {
        _pendingRequest?.Cancel();
        var cts = new CancellationTokenSource();
        _pendingRequest = cts;

        if (_previewWindow is null)
        {
            return;
        }

        if (path is null)
        {
            _previewWindow.ShowPlaceholder("Datei im Explorer markieren, um die Vorschau zu sehen.");
            return;
        }

        try
        {
            var copyResult = await _cache.GetLocalCopyAsync(path, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            if (!copyResult.Success)
            {
                _previewWindow.ShowPlaceholder($"Vorschau nicht verfügbar:\n{copyResult.FailureReason}");
                return;
            }

            var content = await _renderer.BuildAsync(copyResult.LocalPath!, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            await _previewWindow.ShowContentAsync(content);
        }
        catch (OperationCanceledException)
        {
            // Nutzer hat schon weitergeklickt, dieser Request ist obsolet - kein Fehler.
        }
        catch (Exception ex)
        {
            _previewWindow.ShowPlaceholder($"Unerwarteter Fehler bei der Vorschau: {ex.Message}");
        }
    }

    private void OnForegroundExplorerWindowChanged(IntPtr? hwnd)
    {
        if (_previewWindow is null)
        {
            return;
        }

        if (hwnd is { } h)
        {
            _previewWindow.DockBeside(h);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _watcher?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
