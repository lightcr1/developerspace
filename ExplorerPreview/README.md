# Explorer Preview

Immer sichtbare Datei-Vorschau für Windows — ohne Leertaste, ohne Windows'
"Diese Datei könnte Ihren Computer beschädigen"-Warnung, und robust gegenüber
langsamen NAS-/Netzlaufwerken.

Das Repo enthält zwei Varianten, die sich dieselbe Rendering-Logik
(`ExplorerPreview.Core`) teilen:

1. **`ExplorerPreview`** — schwebendes Fenster, das neben dem aktiven
   Explorer-Fenster mitläuft. Einfach zu starten (`dotnet run`), kein Setup.
2. **`ExplorerPreview.Handler`** — ein echter Windows-`IPreviewHandler`, den
   Explorer direkt in seine eingebaute Vorschauleiste lädt (Ansicht →
   Vorschaufenster). Kein Extra-Fenster, aber deutlich mehr Setup (COM-
   Registrierung) und fehleranfälliger beim ersten Einrichten.

> **Status:** Variante 1 wurde erfolgreich gebaut und läuft (Stand: erste
> Live-Tests). Variante 2 (`ExplorerPreview.Handler`) ist **komplett
> ungetestet** — COM-Registrierung von Windows-Shell-Erweiterungen lässt sich
> praktisch nicht ohne echtes Windows verifizieren. Rechne mit mehreren
> Debug-Runden, siehe Abschnitt "Preview-Handler debuggen" unten.

## Was es tut

- Kopiert Dateien **immer zuerst lokal** (Cache unter
  `%LOCALAPPDATA%\ExplorerPreview\cache`) mit Timeout + einem Retry, bevor
  gerendert wird — hängt nicht, wenn das NAS langsam/offline ist.
- Rendert selbst statt sich auf Windows-eigene Preview-Handler zu verlassen:
  - Bilder, PDF, Video → direkt über WebView2 (Edge-Engine)
  - Text/Code → als HTML-Textblock
  - Office-Dokumente (docx/xlsx/pptx/...) → Konvertierung zu PDF über eine
    lokale LibreOffice-Installation (headless, isoliertes Profil, Timeout),
    danach Anzeige über WebView2
- Weil die MOTW-Prüfung der Office-eigenen Handler nie aufgerufen wird,
  erscheint die "Computer beschädigt"-Warnung nicht — **siehe
  Sicherheitsabschnitt unten**, bewusster Trade-off, kein Bug.

## Voraussetzungen

- Windows 10/11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)
  (auf den meisten aktuellen Windows-Installationen bereits vorhanden)
- Für Office-Vorschau: [LibreOffice](https://www.libreoffice.org/) installiert
  unter dem in `AppSettings.LibreOfficePath` hinterlegten Pfad (Standard:
  `C:\Program Files\LibreOffice\program\soffice.exe`). Ohne LibreOffice
  funktioniert alles andere trotzdem, Office-Dateien zeigen dann eine
  Fehlermeldung statt Absturz.

## Variante 1: Schwebendes Fenster

```powershell
cd ExplorerPreview
dotnet build .\ExplorerPreview.sln
dotnet run --project .\src\ExplorerPreview\ExplorerPreview.csproj
```

Läuft als Tray-Icon (Rechtsklick → Beenden). Explorer-Fenster öffnen, eine
einzelne Datei markieren → Vorschau erscheint rechts daneben.

**Bekannte Einschränkungen:**
- Kein Multi-Monitor-Clamping (Fenster kann am Bildschirmrand überstehen).
- Folgt immer dem Explorer-Fenster im Vordergrund, nicht mehreren gleichzeitig.
- Video spielt die Originaldatei per `<video>`-Tag statt Thumbnail — bei
  großen Dateien über NAS wartet man auf die volle lokale Kopie.

## Variante 2: Echter Preview-Handler (in Explorer eingebettet)

### Bauen

```powershell
cd ExplorerPreview
dotnet publish .\src\ExplorerPreview.Handler\ExplorerPreview.Handler.csproj -c Release -r win-x64
```

Das erzeugt u. a. `ExplorerPreview.Handler.comhost.dll` unter
`src\ExplorerPreview.Handler\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\`.
Diese Datei ist die eigentliche COM-Server-DLL.

### Registrieren

**PowerShell als Administrator:**

```powershell
cd ExplorerPreview\scripts
.\Register-PreviewHandler.ps1 -Extensions .log,.csv
```

Bewusst mit einer unkritischen Testendung anfangen (`.log`, `.csv` haben
i. d. R. noch keinen registrierten Preview-Handler) — nicht direkt `.pdf`,
denn das **überschreibt systemweit** den bestehenden Handler (Adobe/Edge)
für diese Endung. Erst wenn das Grundprinzip nachweislich funktioniert,
gezielt auf `.pdf`/`.docx`/etc. ausweiten.

```powershell
Stop-Process -Name explorer -Force; Start-Process explorer
```

Danach: Explorer öffnen, Ansicht → Vorschaufenster aktivieren, eine `.log`-
oder `.csv`-Datei markieren.

### Deregistrieren

```powershell
.\Unregister-PreviewHandler.ps1 -Extensions .log,.csv
```

### Preview-Handler debuggen

Das ist der unangenehme Teil — es gibt kaum hilfreiche Fehlermeldungen,
wenn etwas nicht klappt:

- **Gar nichts passiert / leeres Vorschaufeld:** Meist fehlt der Eintrag in
  der "genehmigten Handler"-Liste (`HKLM:\SOFTWARE\Microsoft\Windows\
  CurrentVersion\PreviewHandlers`) — das Skript trägt den zwar ein, aber
  prüfen lohnt sich (`Get-ItemProperty` auf den Key).
- **Explorer stürzt ab / hängt:** Da der Handler über `AppID`/`DllSurrogate`
  im separaten `prevhost.exe`-Prozess läuft, sollte ein Fehler im Handler
  *nicht* Explorer selbst mitreißen — im Task-Manager nach `prevhost.exe`
  suchen, ob der hängt/abstürzt, statt `explorer.exe` zu beschuldigen.
- **`dotnet publish` findet `EnableComHosting` nicht / keine `.comhost.dll`
  erzeugt:** Sicherstellen, dass `-r win-x64` mit angegeben wird (Self-
  Contained + RuntimeIdentifier sind für COM-Hosting in diesem Setup nötig).
- **Falsche Bitness:** `prevhost.exe` existiert in 32- und 64-Bit-Varianten
  (`SysWOW64` vs. `System32`). Diese Registrierung ist konsequent auf x64
  ausgelegt (`Platforms x64`, `win-x64`) — auf einem x64-Windows sollte das
  automatisch die richtige Variante treffen.
- **Process Monitor** (Sysinternals) ist das mit Abstand nützlichste Tool
  hier: Filter auf `prevhost.exe`, zeigt genau, welche Registry-Keys gesucht
  und ob die DLL überhaupt geladen wird.

## Sicherheit: MOTW-Warnung & Office-Dateien

Die native Windows-Warnung ("Diese Datei könnte Ihren Computer beschädigen")
kommt vom Mark-of-the-Web-Mechanismus (`Zone.Identifier`) und wird von den
*Windows-eigenen* Preview-Handlern (v. a. Office) aktiv geprüft, um Exploits
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
src/ExplorerPreview.Core/        Geteilte Logik (von beiden Varianten genutzt)
  AppSettings.cs                 Zentrale Konfiguration
  Cache/PreviewCache.cs          NAS-sicherer lokaler Kopie-Cache (Timeout/Retry)
  Rendering/RendererSelector.cs  Entscheidet Renderer nach Dateityp
  Rendering/OfficeConverter.cs   LibreOffice-headless-Konvertierung
  Native/NativeMethods.cs        Win32 P/Invoke

src/ExplorerPreview/             Variante 1: schwebendes Fenster
  App.xaml(.cs)                  Einstiegspunkt, Tray-Icon, Verdrahtung
  PreviewWindow.xaml(.cs)        Fenster (WebView2-Host)
  Shell/SelectionWatcher.cs      Explorer-Auswahl per COM-Polling

src/ExplorerPreview.Handler/     Variante 2: echter IPreviewHandler
  Interop.cs                     COM-Interface-Deklarationen (IPreviewHandler etc.)
  ExplorerPreviewHandler.cs      Die eigentliche Handler-Implementierung

scripts/
  Register-PreviewHandler.ps1    COM-/Registry-Registrierung (Admin nötig)
  Unregister-PreviewHandler.ps1  Gegenstück
```
