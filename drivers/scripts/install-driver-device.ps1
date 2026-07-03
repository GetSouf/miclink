# Creates MicLink virtual audio device and binds driver from store.
# Run as Administrator AFTER sign-driver-package.ps1 and pnputil.
$ErrorActionPreference = "Stop"

$devgen = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\Tools" -Recurse -Filter "devgen.exe" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "\\x64\\" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $devgen) {
    Write-Error "devgen.exe not found. Install WDK."
}

$inf = Resolve-Path (Join-Path $PSScriptRoot "..\..\Pc\MicLinkWinUI\MicLinkWinUI\Assets\Driver\MicLinkVirtualAudio.inf")
$hwid = "ROOT\VirtualAudioDriver"

Write-Host "Creating root device: $hwid"
& $devgen /add /bus ROOT /hardwareid $hwid
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Installing driver from $inf"
pnputil /add-driver $inf /install
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "OK. Check MicLink Microphone in Windows Sound settings." -ForegroundColor Green
