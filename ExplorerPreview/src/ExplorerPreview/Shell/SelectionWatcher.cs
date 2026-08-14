using System;
using System.Windows.Threading;
using ExplorerPreview.Native;

namespace ExplorerPreview.Shell;

/// <summary>
/// Beobachtet das aktuell im Vordergrund stehende Explorer-Fenster und meldet,
/// welche einzelne Datei dort gerade markiert ist.
///
/// Design-Entscheidung: Polling statt echtem COM-Event.
/// Es gibt keine öffentlich dokumentierte, zuverlässig über alle Windows-Versionen
/// funktionierende "SelectionChanged"-Benachrichtigung für Explorer-Fenster.
/// Ein Timer mit kurzem Intervall (Default 200ms, siehe AppSettings) ist der
/// Ansatz, den auch vergleichbare Open-Source-Tools nutzen - spürbar günstiger
/// als es klingt, weil nur eine COM-Property abgefragt wird.
/// </summary>
public sealed class SelectionWatcher : IDisposable
{
    public event Action<string?>? SelectionChanged;

    /// <summary>
    /// Feuert bei jedem Poll-Tick mit dem aktuellen Vordergrund-Explorer-Fenster
    /// (oder null, wenn keins im Vordergrund ist) - genutzt, um das Vorschaufenster
    /// live neben dem Explorer-Fenster mitzuziehen, auch wenn sich die Auswahl
    /// selbst nicht ändert (z.B. beim Verschieben des Explorer-Fensters).
    /// </summary>
    public event Action<IntPtr?>? ForegroundExplorerWindowChanged;

    private readonly DispatcherTimer _timer;
    private readonly dynamic _shellApplication;
    private string? _lastPath;

    public SelectionWatcher()
    {
        // Late-Binding über dynamic statt starker Typisierung auf die generierten
        // Interop-Typen: robuster gegenüber leichten Signaturunterschieden zwischen
        // Windows-Versionen, und die genauen Interop-Typnamen (IShellFolderViewDual
        // vs. ...Dual2 vs. ...Dual3) variieren je nach installierter Shell32-Version.
        var shellType = Type.GetTypeFromProgID("Shell.Application")
            ?? throw new InvalidOperationException("Shell.Application COM-Objekt nicht verfügbar.");
        _shellApplication = Activator.CreateInstance(shellType)!;

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = AppSettings.SelectionPollInterval
        };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void Poll()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        bool isExplorer = foreground != IntPtr.Zero
            && NativeMethods.GetClassNameOf(foreground) == "CabinetWClass";
        ForegroundExplorerWindowChanged?.Invoke(isExplorer ? foreground : null);

        string? current;
        try
        {
            current = isExplorer ? GetSingleSelectedFileInWindow(foreground) : null;
        }
        catch (Exception)
        {
            // Explorer-Fenster kann jederzeit geschlossen werden, COM-Referenzen
            // dann ungültig werden - das ist transient und kein Programmfehler.
            current = null;
        }

        if (current != _lastPath)
        {
            _lastPath = current;
            SelectionChanged?.Invoke(current);
        }
    }

    private string? GetSingleSelectedFileInWindow(IntPtr foreground)
    {
        dynamic windows = _shellApplication.Windows();
        int count = windows.Count;
        for (int i = 0; i < count; i++)
        {
            dynamic window = windows.Item(i);
            if (window is null)
            {
                continue;
            }

            long hwnd;
            try
            {
                hwnd = (long)window.HWND;
            }
            catch (Exception)
            {
                continue; // kein Fenster mit HWND-Property (z.B. IE-Reste), überspringen
            }

            if (hwnd != foreground.ToInt64())
            {
                continue;
            }

            dynamic? document = window.Document;
            if (document is null)
            {
                return null;
            }

            dynamic selectedItems = document.SelectedItems();
            int selectedCount = selectedItems.Count;
            if (selectedCount != 1)
            {
                // Bewusst: bei 0 oder >1 markierten Dateien keine Vorschau
                // (analog zu QuickLook / Finder Quick Look Verhalten).
                return null;
            }

            dynamic item = selectedItems.Item(0);
            string path = item.Path;
            return path;
        }

        return null;
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
