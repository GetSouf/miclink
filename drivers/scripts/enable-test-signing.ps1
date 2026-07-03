# Requires Administrator. Enables test signing and shows current state.
#Usage: Right-click PowerShell -> Run as administrator -> .\enable-test-signing.ps1

$ErrorActionPreference = "Stop"
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host ""
    Write-Host "ERROR: Run PowerShell as Administrator." -ForegroundColor Red
    Write-Host "  Start -> PowerShell -> Right click -> Run as administrator"
    exit 1
}

Write-Host "Current boot configuration:" -ForegroundColor Cyan
& bcdedit /enum "{current}" | Select-String -Pattern "testsigning|nointegritychecks|loadoptions"

Write-Host ""
Write-Host "Enabling test signing..." -ForegroundColor Yellow
& bcdedit /set testsigning on
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "After reboot you should see 'Test Mode' watermark in the corner." -ForegroundColor Green
Write-Host "Then install MicLink driver again from Settings." -ForegroundColor Green
Write-Host ""
$reboot = Read-Host "Reboot now? (Y/N)"
if ($reboot -eq "Y" -or $reboot -eq "y") {
    shutdown /r /t 10 /c "MicLink: test signing enabled"
    Write-Host "Rebooting in 10 seconds..."
}
