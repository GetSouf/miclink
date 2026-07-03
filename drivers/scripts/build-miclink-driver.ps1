# Build MicLinkVirtualAudio.sys (requires WDK + Visual Studio C++)
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$root = Split-Path -Parent $PSScriptRoot
$upstream = Join-Path $root "MicLinkVirtualAudio\upstream"
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1

if (-not $msbuild) {
    Write-Error "MSBuild not found. Install Visual Studio with C++ workload."
}

$kitsRoot = (Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows Kits\Installed Roots" -ErrorAction SilentlyContinue).KitsRoot10
if (-not $kitsRoot) {
    $kitsRoot = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots" -ErrorAction SilentlyContinue).KitsRoot10
}

$wdkBuild = if ($kitsRoot) { Join-Path $kitsRoot "build" } else { $null }
$wdkProps = if ($kitsRoot) { Get-ChildItem -Path $kitsRoot -Recurse -Filter "WindowsDriver.Default.props" -ErrorAction SilentlyContinue | Select-Object -First 1 } else { $null }

if (-not $kitsRoot) {
    Write-Host "Windows SDK not found." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $wdkBuild) -and -not $wdkProps) {
    Write-Host ""
    Write-Host "Windows SDK est ($kitsRoot), no WDK driver build." -ForegroundColor Yellow
    Write-Host "SDK alone is not enough - install WDK separately:" -ForegroundColor Red
    Write-Host "  winget install Microsoft.WindowsWDK.10.0.28000"
    Write-Host "  https://learn.microsoft.com/windows-hardware/drivers/download-the-wdk"
    Write-Host "Then reboot PC and run this script again."
    exit 1
}

Write-Host "WDK/SDK: $kitsRoot"
Write-Host "Building MicLinkVirtualAudio (Release x64)..."

$bat = Join-Path $PSScriptRoot "build-miclink-driver.bat"
if (Test-Path $bat) {
    & cmd /c "`"$bat`""
    exit $LASTEXITCODE
}

& $msbuild (Join-Path $upstream "VirtualAudioDriver.sln") /p:Configuration=Release /p:Platform=x64 /p:SkipPackageVerification=true /p:ApiValidator_Enable=false /p:SignMode=Off /m
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$sys = Get-ChildItem -Path $upstream -Recurse -Filter "MicLinkVirtualAudio.sys" | Select-Object -First 1
if (-not $sys) {
    Write-Error "MicLinkVirtualAudio.sys not found after build"
}

$infSrc = Get-ChildItem -Path $upstream -Recurse -Filter "VirtualAudioDriver.inf" | Select-Object -First 1
$destDir = Join-Path $root "..\Pc\MicLinkWinUI\MicLinkWinUI\Assets\Driver"
$installDir = Join-Path $root "MicLinkVirtualAudio\install"
New-Item -ItemType Directory -Force -Path $destDir, $installDir | Out-Null

Copy-Item $sys.FullName (Join-Path $destDir "MicLinkVirtualAudio.sys") -Force
Copy-Item $sys.FullName (Join-Path $installDir "MicLinkVirtualAudio.sys") -Force

if ($infSrc) {
    $infText = Get-Content $infSrc.FullName -Raw
    $infText = $infText -replace "VirtualAudioDriver\.sys", "MicLinkVirtualAudio.sys"
    $infText = $infText -replace "Virtual Audio Driver by MTT", "MicLink Virtual Audio"
    Set-Content (Join-Path $destDir "MicLinkVirtualAudio.inf") $infText -Encoding Unicode
    Set-Content (Join-Path $installDir "MicLinkVirtualAudio.inf") $infText -Encoding Unicode
}

Write-Host ""
Write-Host "OK: $($sys.FullName)" -ForegroundColor Green
Write-Host "Copied to Assets/Driver. Restart MicLink and install driver (UAC)."
Write-Host ""
Write-Host "Test signing (run PowerShell AS ADMINISTRATOR):" -ForegroundColor Yellow
Write-Host "  bcdedit /set testsigning on"
Write-Host "Then reboot."
