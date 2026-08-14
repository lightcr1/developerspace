#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Registriert ExplorerPreview.Handler als echten Windows-Shell-Preview-Handler.

.DESCRIPTION
  Trägt die COM-Registrierung (CLSID, InprocServer32, AppID-Surrogat) sowie die
  Zuordnung zu Dateiendungen in die Registry ein, und trägt den Handler in die
  von Explorer geprüfte "genehmigte Handler"-Liste ein (ohne diesen Eintrag
  ignoriert Explorer aus Sicherheitsgründen unbekannte Preview-Handler
  stillschweigend - das ist einer der häufigsten Stolpersteine bei sowas).

.PARAMETER Extensions
  Dateiendungen, für die dieser Handler zuständig sein soll (mit führendem Punkt).
  VORSICHT: Das ÜBERSCHREIBT einen ggf. schon vorhandenen Preview-Handler für
  diese Endung systemweit (z.B. Adobe/Edge für .pdf). Zum ersten Testen daher
  eine Endung ohne bestehenden Handler empfehlenswert (z.B. .log, .csv),
  bevor man z.B. .pdf umbiegt.

.EXAMPLE
  .\Register-PreviewHandler.ps1 -Extensions .log,.csv
  .\Register-PreviewHandler.ps1 -Extensions .pdf -DllPath "C:\SandboxShare\ExplorerPreview\ExplorerPreview\src\ExplorerPreview.Handler\bin\Debug\net8.0-windows10.0.19041.0\win-x64\ExplorerPreview.Handler.comhost.dll"
#>
param(
    [Parameter(Mandatory = $true)]
    [string[]]$Extensions,

    [string]$DllPath
)

$ErrorActionPreference = "Stop"

$Clsid = "{E3A2C7D4-9F1B-4C8E-8A2D-6B1F4E7C9A02}"
$PreviewHandlerCategoryClsid = "{8895b1c6-b41f-4c1c-a562-0d564250836f}"
$HandlerName = "ExplorerPreview Handler"

if (-not $DllPath) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $candidates = Get-ChildItem -Path (Join-Path $repoRoot "src\ExplorerPreview.Handler\bin") `
        -Filter "ExplorerPreview.Handler.comhost.dll" -Recurse -ErrorAction SilentlyContinue
    if (-not $candidates) {
        throw "Keine ExplorerPreview.Handler.comhost.dll gefunden. Erst 'dotnet build' im Handler-Projekt ausführen oder -DllPath explizit angeben."
    }
    $DllPath = ($candidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

if (-not (Test-Path $DllPath)) {
    throw "DLL nicht gefunden: $DllPath"
}

Write-Host "Registriere Preview-Handler:" -ForegroundColor Cyan
Write-Host "  CLSID: $Clsid"
Write-Host "  DLL:   $DllPath"

# --- CLSID + InprocServer32 ---
$clsidKey = "HKLM:\SOFTWARE\Classes\CLSID\$Clsid"
New-Item -Path $clsidKey -Force | Out-Null
Set-ItemProperty -Path $clsidKey -Name "(Default)" -Value $HandlerName

$inprocKey = "$clsidKey\InprocServer32"
New-Item -Path $inprocKey -Force | Out-Null
Set-ItemProperty -Path $inprocKey -Name "(Default)" -Value $DllPath
Set-ItemProperty -Path $inprocKey -Name "ThreadingModel" -Value "Apartment"

# --- AppID: erzwingt Hosting im prevhost.exe-Surrogatprozess statt in-process
#     in Explorer selbst - das ist der von Microsoft empfohlene, robustere Weg
#     (ein Absturz im Handler reißt dann nicht explorer.exe mit runter). ---
Set-ItemProperty -Path $clsidKey -Name "AppID" -Value $Clsid
$appIdKey = "HKLM:\SOFTWARE\Classes\AppID\$Clsid"
New-Item -Path $appIdKey -Force | Out-Null
Set-ItemProperty -Path $appIdKey -Name "DllSurrogate" -Value ""

# --- In die von Explorer geprüfte Positivliste eintragen ---
$approvedKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\PreviewHandlers"
New-Item -Path $approvedKey -Force | Out-Null
Set-ItemProperty -Path $approvedKey -Name $Clsid -Value $HandlerName

# --- Pro Dateiendung eintragen ---
foreach ($ext in $Extensions) {
    if (-not $ext.StartsWith(".")) { $ext = ".$ext" }
    $shellexKey = "HKLM:\SOFTWARE\Classes\$ext\shellex\$PreviewHandlerCategoryClsid"
    New-Item -Path $shellexKey -Force | Out-Null
    Set-ItemProperty -Path $shellexKey -Name "(Default)" -Value $Clsid
    Write-Host "  Registriert für: $ext" -ForegroundColor Green
}

Write-Host ""
Write-Host "Fertig. Explorer neu starten, damit die Änderung sicher greift:" -ForegroundColor Yellow
Write-Host "  Stop-Process -Name explorer -Force; Start-Process explorer"
