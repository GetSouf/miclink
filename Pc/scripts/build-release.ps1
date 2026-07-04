param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipDriver,
    [switch]$SkipAndroid,
    [switch]$SignDriver
)

$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$version = '0.1.0'
$releaseDir = Join-Path $root "release\v$version"
$publishDir = Join-Path $root "Pc\MicLinkWinUI\MicLinkWinUI\bin\$Configuration\net8.0-windows10.0.19041.0\win-x64\publish"
$apkPath = Join-Path $root "Mobile\miclink\build\app\outputs\flutter-apk\app-release.apk"
$flutter = 'C:\Users\kompik\dev\flutter\bin\flutter.bat'
if (-not (Test-Path $flutter)) {
    $flutterCmd = Get-Command flutter -ErrorAction SilentlyContinue
    if ($flutterCmd) { $flutter = $flutterCmd.Source }
}

Write-Host "=== MicLink release v$version ===" -ForegroundColor Cyan

if (-not $SkipDriver) {
    Write-Host "`n[1/4] Driver..." -ForegroundColor Yellow
    cmd /c "`"$root\drivers\scripts\build-miclink-driver.bat`""
    if ($SignDriver) {
        Write-Host "Signing driver (admin required)..." -ForegroundColor Yellow
        & "$root\drivers\scripts\sign-driver-package.ps1"
    }
}

Write-Host "`n[2/4] PC publish ($Configuration)..." -ForegroundColor Yellow
& "$root\Pc\scripts\publish-portable.ps1" -Configuration $Configuration

if (-not (Test-Path "$publishDir\Assets\Driver\MicLinkVirtualAudio.sys")) {
    Write-Error "Driver not in publish folder. Check MicLinkWinUI.csproj CopyToPublishDirectory."
}

if (-not $SkipAndroid) {
    Write-Host "`n[3/4] Android APK..." -ForegroundColor Yellow
    Push-Location (Join-Path $root 'Mobile\miclink')
    try {
        if (-not (Test-Path $flutter)) { Write-Error "Flutter not found. Set path in build-release.ps1 or add to PATH." }
        & $flutter pub get
        & $flutter build apk --release
    }
    finally {
        Pop-Location
    }
}

Write-Host "`n[4/4] Packaging..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

$winZip = Join-Path $releaseDir "MicLink-win-x64-v$version.zip"
if (Test-Path $winZip) { Remove-Item $winZip -Force }
Compress-Archive -Path "$publishDir\*" -DestinationPath $winZip -CompressionLevel Optimal

if (Test-Path $apkPath) {
    Copy-Item $apkPath (Join-Path $releaseDir "MicLink-android-v$version.apk") -Force
}

$notes = @"
MicLink v$version
Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm')

Windows (x64):
  MicLink-win-x64-v$version.zip — portable, self-contained (.NET не нужен)
  Распаковать → MicLinkWinUI.exe
  Драйвер: Assets\Driver\ (установка при первом запуске или install-driver.ps1)
  Требуется Test Mode для unsigned driver: bcdedit /set testsigning on

Android:
  MicLink-android-v$version.apk
  Установить на телефон, тот же Wi‑Fi что и ПК

Подробнее: README.md, TESTING.md
"@
Set-Content -Path (Join-Path $releaseDir 'RELEASE-NOTES.txt') -Value $notes -Encoding UTF8

Write-Host ""
Write-Host "Done:" -ForegroundColor Green
Write-Host "  $releaseDir"
Get-ChildItem $releaseDir | ForEach-Object {
    $mb = [math]::Round($_.Length / 1MB, 1)
    Write-Host "  $($_.Name)  ($mb MB)"
}
