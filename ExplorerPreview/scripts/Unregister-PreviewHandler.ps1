#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Removes the ExplorerPreview.Handler registration entirely
  (counterpart to Register-PreviewHandler.ps1).

.PARAMETER Extensions
  File extensions whose shellex association with this handler should be removed.
#>
param(
    [Parameter(Mandatory = $true)]
    [string[]]$Extensions
)

$ErrorActionPreference = "Stop"

$Clsid = "{E3A2C7D4-9F1B-4C8E-8A2D-6B1F4E7C9A02}"
$PreviewHandlerCategoryClsid = "{8895b1c6-b41f-4c1c-a562-0d564250836f}"

foreach ($ext in $Extensions) {
    if (-not $ext.StartsWith(".")) { $ext = ".$ext" }
    $shellexKey = "HKLM:\SOFTWARE\Classes\$ext\shellex\$PreviewHandlerCategoryClsid"
    if (Test-Path $shellexKey) {
        Remove-Item -Path $shellexKey -Force
        Write-Host "Removed for: $ext" -ForegroundColor Green
    }
}

Remove-Item -Path "HKLM:\SOFTWARE\Classes\CLSID\$Clsid" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "HKLM:\SOFTWARE\Classes\AppID\$Clsid" -Recurse -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\PreviewHandlers" -Name $Clsid -ErrorAction SilentlyContinue

Write-Host "Handler registration removed. Restart Explorer:" -ForegroundColor Yellow
Write-Host "  Stop-Process -Name explorer -Force; Start-Process explorer"
