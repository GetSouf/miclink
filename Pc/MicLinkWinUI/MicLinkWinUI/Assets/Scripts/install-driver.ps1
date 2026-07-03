# Installs MicLink Virtual Audio driver from the app bundle.
# Must run as Administrator (UAC). Called by MicLink app or MicLinkSetup.exe.

$ErrorActionPreference = 'Stop'

$driverDir = Join-Path $PSScriptRoot '..\Driver'
$inf = Join-Path $driverDir 'MicLinkVirtualAudio.inf'
$sys = Join-Path $driverDir 'MicLinkVirtualAudio.sys'
$hwid = 'ROOT\VirtualAudioDriver'

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p = New-Object Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-DevGen {
    $kitsTools = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\Tools'
    if (-not (Test-Path $kitsTools)) { return $null }
    return Get-ChildItem $kitsTools -Recurse -Filter 'devgen.exe' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

if (-not (Test-Admin)) {
    Write-Error 'Run as Administrator.'
}

if (-not ((Test-Path $inf) -and (Test-Path $sys))) {
    Write-Error "Driver package not found in: $driverDir"
}

Write-Host 'MicLink: installing virtual microphone driver...'

$devgen = Get-DevGen
if ($devgen) {
    Write-Host "Creating device ($hwid)..."
    & $devgen /add /bus ROOT /hardwareid $hwid
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Device may already exist — continuing with pnputil.' -ForegroundColor Yellow
    }
} else {
    Write-Host 'devgen.exe not found (WDK). Trying pnputil only...' -ForegroundColor Yellow
}

Write-Host "Installing driver: $inf"
pnputil /add-driver $inf /install
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ''
Write-Host 'OK. Open Windows Sound settings — input device MicLink Virtual Audio.' -ForegroundColor Green
