$ErrorActionPreference = "Stop"

Write-Host "ExplorerPreview Sandbox-Setup startet..." -ForegroundColor Cyan

# Scripts fuer diesen Benutzer erlauben (keine Admin-Rechte noetig, gilt nur
# fuer diese Sandbox-Sitzung sowieso, da nichts dauerhaft ist).
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy Bypass -Force

# .NET 8 SDK x64 automatisch per offiziellem Microsoft-Installationsskript
# holen (kein manueller Installer-Download noetig, kein Risiko den falschen
# x86-Installer zu erwischen wie beim manuellen Weg).
$installDir = "C:\dotnet"
if (-not (Test-Path "$installDir\dotnet.exe")) {
    Write-Host "Installiere .NET SDK 8.0 x64 nach $installDir ..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile "$env:TEMP\dotnet-install.ps1"
    & "$env:TEMP\dotnet-install.ps1" -Channel 8.0 -Architecture x64 -InstallDir $installDir
} else {
    Write-Host ".NET SDK bereits vorhanden unter $installDir" -ForegroundColor Green
}

# WICHTIG: sorgt dafuer, dass JEDES neu geoeffnete PowerShell-Fenster dotnet
# automatisch im PATH hat - nicht nur dieses eine hier. Windows gibt
# Umgebungsvariablen-Aenderungen sonst nicht an schon laufende Prozesse
# (explorer.exe) weiter, weshalb ein zweites Terminal-Fenster "dotnet nicht
# erkannt" gemeldet haette, obwohl es installiert ist - genau das Problem,
# das wir vorher haendisch mit $env:Path += ... umschiffen mussten.
if (-not (Test-Path $PROFILE)) {
    New-Item -ItemType File -Path $PROFILE -Force | Out-Null
}
$profileContent = Get-Content $PROFILE -ErrorAction SilentlyContinue
if (-not ($profileContent | Select-String -SimpleMatch $installDir -Quiet)) {
    Add-Content -Path $PROFILE -Value "`$env:Path += ';$installDir'"
}

$env:Path += ";$installDir"

Write-Host ""
Write-Host "Setup fertig. dotnet-Version:" -ForegroundColor Green
& "$installDir\dotnet.exe" --version
