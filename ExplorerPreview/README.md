# Explorer Preview

Immer sichtbare Datei-Vorschau, die neben dem aktiven Windows-Explorer-Fenster
mitläuft — ohne Leertaste, ohne Windows' "Diese Datei könnte Ihren Computer
beschädigen"-Warnung, und robust gegenüber langsamen NAS-/Netzlaufwerken.

> **Status:** v0.1, in diesem Repo aus einer Linux-Umgebung heraus geschrieben
> und **nicht kompiliert/getestet**. Windows + Visual Studio / `dotnet build`
> zum ersten Build sind nötig. Bitte Bugs/Abweichungen zurückmelden — das ist
> ein reales Grundgerüst, kein Pseudocode, aber der erste Build-Durchlauf wird
> vermutlich noch kleinere Anpassungen brauchen (typische Kandidaten unten).

## Was es tut

- Pollt (alle 200ms) das aktive Explorer-Fenster über COM auf die aktuell
  markierte Datei (kein Windows-Hook, keine Admin-Rechte nötig).
- Zeigt die Vorschau in einem eigenen, neben dem Explorer-Fenster
  angedockten Fenster (kein Leertastendruck).
- Kopiert Dateien **immer zuerst lokal** (Cache unter
  `%LOCALAPPDATA%\ExplorerPreview\cache`) mit Timeout + einem Retry, bevor
  gerendert wird — hängt die UI nicht ein, wenn das NAS langsam/offline ist.
- Rendert selbst statt über Windows-`IPreviewHandler`:
  - Bilder, PDF, Video → direkt über WebView2 (Edge-Engine)
  - Text/Code → als HTML-Textblock
  - Office-Dokumente (docx/xlsx/pptx/...) → Konvertierung zu PDF über eine
    lokale LibreOffice-Installation (headless, isoliertes Profil, Timeout),
    danach Anzeige über WebView2
- Weil die MOTW-Prüfung nie aufgerufen wird (wir nutzen nicht die
  Office-eigenen Preview-Handler), erscheint die "Computer beschädigt"-
  Warnung nicht — **siehe Sicherheitsabschnitt unten**, das ist ein bewusster
  Trade-off, kein Bug.

## Voraussetzungen

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)
  (auf den meisten aktuellen Windows-Installationen bereits vorhanden)
- Für Office-Vorschau: [LibreOffice](https://www.libreoffice.org/) installiert
  unter dem in `AppSettings.LibreOfficePath` hinterlegten Pfad (Standard:
  `C:\Program Files\LibreOffice\program\soffice.exe`). Ohne LibreOffice
  funktioniert alles andere trotzdem, Office-Dateien zeigen dann eine
  Fehlermeldung statt Absturz.

## Bauen & Starten

```powershell
cd ExplorerPreview
dotnet build .\ExplorerPreview.sln
dotnet run --project .\src\ExplorerPreview\ExplorerPreview.csproj
```

Die App läuft als Tray-Icon (rechtsklick → Beenden). Ein Explorer-Fenster
öffnen, eine einzelne Datei markieren → Vorschau erscheint rechts daneben.

## Bekannte Grenzen / erwartete erste Baustellen

- **COM-Interop-Namen (`SHDocVw`, `Shell32`)**: Die `<COMReference>`-Einträge
  im `.csproj` binden die Windows-eigenen Typebibliotheken zur Build-Zeit ein.
  Falls `dotnet build` hier meckert, sind ggf. leicht andere Registrierungen
  auf dem Zielsystem nötig — Details siehe Kommentare in `SelectionWatcher.cs`.
  Der Code nutzt bewusst `dynamic` statt starker Typisierung, um Versions-
  unterschiede der generierten Interop-Typen abzufedern.
- **Docking-Logik** (`PreviewWindow.DockBeside`) ist ein einfacher
  `SetWindowPos` rechts vom Explorer-Fenster — kein echtes Einbetten *in*
  `explorer.exe` (siehe Sicherheits-/Architekturdiskussion: das wäre DLL-
  Injection in einen Microsoft-Prozess, instabil und nicht unterstützt).
  Bei mehreren offenen Explorer-Fenstern folgt die Vorschau immer dem
  Fenster im Vordergrund.
- **Kein echtes Multi-Monitor-Clamping**: Wenn das Explorer-Fenster ganz
  rechts am Bildschirmrand steht, kann das Vorschaufenster über den
  sichtbaren Bereich hinausragen. Einfacher Folge-Fix: Bildschirmgrenzen in
  `DockBeside` berücksichtigen.
- **Video-Vorschau** spielt die Originaldatei per `<video>`-Tag ab statt ein
  Thumbnail zu erzeugen — einfacher und robuster, aber bei sehr großen
  Videos über NAS wartet man auf die volle lokale Kopie, bevor die Wiedergabe
  startet.

## Sicherheit: MOTW-Warnung & Office-Dateien

Die native Windows-Warnung ("Diese Datei könnte Ihren Computer beschädigen")
kommt vom Mark-of-the-Web-Mechanismus (`Zone.Identifier`) und wird von den
*Windows-eigenen* Preview-Handlern (v.a. Office) aktiv geprüft, um Exploits
in deren Parsern bei Dateien aus dem Internet/Netzwerk zu verhindern.

Dieses Tool ruft diese Handler nicht auf und sieht die Warnung deshalb nie —
das ist gewollt, aber kein Freifahrtschein:

- Bilder/PDF/Text/Video werden direkt über WebView2 gerendert — risikoarm.
- Office-Dokumente laufen durch eine LibreOffice-Konvertierung, die keine
  Makros ausführt und in einem frischen, isolierten Profil pro Datei läuft,
  mit hartem Timeout gegen pathologische Dateien.
- Das ist **keine vollständige Sandbox** (kein AppContainer/keine VM). Für
  Umgebungen mit wirklich nicht vertrauenswürdigen Quellen wäre ein
  zusätzlicher Isolationslayer (z. B. Ausführung in Windows Sandbox oder
  einem restriktiven Job-Object-Token) der sinnvolle nächste Schritt — bewusst
  als offene Baustelle dokumentiert statt stillschweigend übergangen.
- `AppSettings.EnableOfficePreview = false` deaktiviert die Office-Vorschau
  komplett, falls das im eigenen Bedrohungsmodell zu heikel ist.

## Projektstruktur

```
src/ExplorerPreview/
  App.xaml(.cs)              Einstiegspunkt, Tray-Icon, Verdrahtung
  PreviewWindow.xaml(.cs)     Schwebendes Vorschaufenster (WebView2-Host)
  AppSettings.cs              Zentrale Konfiguration
  Shell/SelectionWatcher.cs   Explorer-Auswahl per COM-Polling
  Cache/PreviewCache.cs       NAS-sicherer lokaler Kopie-Cache (Timeout/Retry)
  Rendering/RendererSelector.cs   Entscheidet Renderer nach Dateityp
  Rendering/OfficeConverter.cs    LibreOffice-headless-Konvertierung
  Native/NativeMethods.cs     Win32 P/Invoke (Fenstererkennung/-positionierung)
```
