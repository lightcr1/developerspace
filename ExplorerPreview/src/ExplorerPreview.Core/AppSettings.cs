using System;
using System.IO;

namespace ExplorerPreview;

/// <summary>
/// Zentrale Einstellungen. Bewusst simpel (keine externe Config-Library),
/// damit man v1 ohne zusätzliche Abhängigkeiten bauen kann.
/// </summary>
public static class AppSettings
{
    /// <summary>Wie lange maximal auf eine Datei-Kopie von NAS/Netzwerk gewartet wird, bevor abgebrochen wird.</summary>
    public static readonly TimeSpan NetworkCopyTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Wie oft der Explorer auf Selektionsänderungen abgefragt wird (Polling, siehe SelectionWatcher).</summary>
    public static readonly TimeSpan SelectionPollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>Maximale Größe des lokalen Vorschau-Caches, bevor älteste Einträge gelöscht werden.</summary>
    public const long MaxCacheBytes = 500L * 1024 * 1024; // 500 MB

    /// <summary>Timeout für die LibreOffice-Konvertierung (Office-Dokumente -> PDF).</summary>
    public static readonly TimeSpan OfficeConversionTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Office-Vorschau (docx/xlsx/pptx/...) benötigt eine lokale LibreOffice-Installation.
    /// Kann auf false gesetzt werden, wenn man Office-Dateien aus Vorsicht grundsätzlich
    /// nicht automatisch konvertieren/anzeigen lassen will.
    /// </summary>
    public static readonly bool EnableOfficePreview = true;

    /// <summary>Pfad zu soffice.exe. Übliche Standardinstallation unter Windows.</summary>
    public const string LibreOfficePath = @"C:\Program Files\LibreOffice\program\soffice.exe";

    public static string CacheDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ExplorerPreview", "cache");
}
