#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Registers ExplorerPreview.Handler as a real Windows shell preview handler.

.DESCRIPTION
  Writes the COM registration (CLSID, InprocServer32, AppID surrogate) plus
  the per-extension association into the registry, and adds the handler to
  the "approved handlers" list Explorer checks (without this entry Explorer
  silently ignores unknown preview handlers for security reasons - this is
  one of the most common gotchas here).

.PARAMETER Extensions
  File extensions this handler should be responsible for (with leading dot).
  WARNING: this OVERWRITES any preview handler already registered for that
  extension system-wide (e.g. Adobe/Edge for .pdf). Recommended to test with
  an extension that has no existing handler first (e.g. .log, .csv) before
  pointing this at .pdf.

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
        throw "No ExplorerPreview.Handler.comhost.dll found. Run 'dotnet publish' on the Handler project first, or pass -DllPath explicitly."
    }
    $DllPath = ($candidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

if (-not (Test-Path $DllPath)) {
    throw "DLL not found: $DllPath"
}

Write-Host "Registering preview handler:" -ForegroundColor Cyan
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

# --- AppID: forces hosting in the prevhost.exe surrogate process instead of
#     in-process inside Explorer itself - the approach Microsoft recommends
#     (a crash in the handler then doesn't take explorer.exe down with it). ---
Set-ItemProperty -Path $clsidKey -Name "AppID" -Value $Clsid
$appIdKey = "HKLM:\SOFTWARE\Classes\AppID\$Clsid"
New-Item -Path $appIdKey -Force | Out-Null
Set-ItemProperty -Path $appIdKey -Name "DllSurrogate" -Value ""

# --- Add to the approved-handlers allowlist Explorer checks ---
$approvedKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\PreviewHandlers"
New-Item -Path $approvedKey -Force | Out-Null
Set-ItemProperty -Path $approvedKey -Name $Clsid -Value $HandlerName

# --- Register per file extension ---
foreach ($ext in $Extensions) {
    if (-not $ext.StartsWith(".")) { $ext = ".$ext" }
    $shellexKey = "HKLM:\SOFTWARE\Classes\$ext\shellex\$PreviewHandlerCategoryClsid"
    New-Item -Path $shellexKey -Force | Out-Null
    Set-ItemProperty -Path $shellexKey -Name "(Default)" -Value $Clsid
    Write-Host "  Registered for: $ext" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Restart Explorer so the change reliably takes effect:" -ForegroundColor Yellow
Write-Host "  Stop-Process -Name explorer -Force; Start-Process explorer"
