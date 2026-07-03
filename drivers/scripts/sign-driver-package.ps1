# Signs MicLinkVirtualAudio.sys + creates/signs .cat (dev test certificate).
# Run as Administrator.
param(
    [string]$DriverDir = (Resolve-Path (Join-Path $PSScriptRoot "..\..\Pc\MicLinkWinUI\MicLinkWinUI\Assets\Driver")).Path
)

$ErrorActionPreference = "Stop"

function Find-KitsTool([string]$Name) {
    $kitsRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (-not (Test-Path $kitsRoot)) {
        return $null
    }

    return Get-ChildItem -Path $kitsRoot -Recurse -Filter $Name -ErrorAction SilentlyContinue |
        Sort-Object { if ($_.FullName -match "\\x64\\") { 0 } elseif ($_.FullName -match "\\x86\\") { 1 } else { 2 } } |
        Select-Object -First 1 -ExpandProperty FullName
}

$inf2cat = Find-KitsTool "Inf2Cat.exe"
if (-not $inf2cat) { $inf2cat = Find-KitsTool "inf2cat.exe" }
$signtool = Find-KitsTool "signtool.exe"

if (-not $inf2cat) {
    Write-Error "Inf2Cat.exe not found. Install WDK: winget install Microsoft.WindowsWDK.10.0.28000"
}
if (-not $signtool) {
    Write-Error "signtool.exe not found. Install WDK/SDK."
}

Write-Host "Using Inf2Cat: $inf2cat"
Write-Host "Using signtool: $signtool"

$sys = Join-Path $DriverDir "MicLinkVirtualAudio.sys"
$inf = Join-Path $DriverDir "MicLinkVirtualAudio.inf"
if (-not (Test-Path $sys)) {
    Write-Error "Missing $sys. Run drivers\scripts\build-miclink-driver.bat first."
}
if (-not (Test-Path $inf)) {
    Write-Error "Missing $inf. Run drivers\scripts\prepare-driver-package.ps1 first."
}

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: Run PowerShell as Administrator." -ForegroundColor Red
    exit 1
}

$certDir = Join-Path $PSScriptRoot "..\MicLinkVirtualAudio\dev-cert"
New-Item -ItemType Directory -Force -Path $certDir | Out-Null
$pfxPath = Join-Path $certDir "MicLinkTest.pfx"
$pfxPassword = "MicLinkDevTest1!"

$cert = Get-ChildItem Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -like "*CN=MicLink Test Driver*" } |
    Select-Object -First 1

if (-not $cert) {
    Write-Host "Creating test code-signing certificate..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate `
        -Subject "CN=MicLink Test Driver" `
        -Type CodeSigningCert `
        -KeyUsage DigitalSignature `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -CertStoreLocation "Cert:\LocalMachine\My" `
        -NotAfter (Get-Date).AddYears(5)

    $securePwd = ConvertTo-SecureString -String $pfxPassword -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePwd | Out-Null
    Import-PfxCertificate -FilePath $pfxPath -Password $securePwd -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
    Import-PfxCertificate -FilePath $pfxPath -Password $securePwd -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null
    Write-Host "Certificate installed to LocalMachine Root and TrustedPublisher"
}
elseif (-not (Test-Path $pfxPath)) {
    $securePwd = ConvertTo-SecureString -String $pfxPassword -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePwd | Out-Null
}

Write-Host "Signing MicLinkVirtualAudio.sys..." -ForegroundColor Cyan
& $signtool sign /v /fd SHA256 /f $pfxPath /p $pfxPassword /tr http://timestamp.digicert.com /td SHA256 $sys
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Inf2Cat needs CatalogFile in [Version] before it runs.
Write-Host "Updating INF with CatalogFile entries..." -ForegroundColor Cyan
$infText = Get-Content $inf -Raw
$infText = $infText -replace "(?m)^CatalogFile(\.ntamd64)?\s*=.*\r?\n", ""
$infText = $infText -replace "(DriverVer[^\r\n]*\r?\n)", "`$1CatalogFile=MicLinkVirtualAudio.cat`r`nCatalogFile.ntamd64=MicLinkVirtualAudio.cat`r`n"
Set-Content -Path $inf -Value $infText -Encoding Unicode

Write-Host "Creating catalog (Inf2Cat)..." -ForegroundColor Cyan
$staging = Join-Path $env:TEMP "MicLinkDriverSign"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force -Path $staging | Out-Null
Copy-Item $sys, $inf -Destination $staging -Force
Push-Location $staging
& $inf2cat /driver:. /os:10_X64 /verbose
$inf2catExit = $LASTEXITCODE
Pop-Location
if ($inf2catExit -ne 0) {
    Write-Error "Inf2Cat failed with exit code $inf2catExit"
}

$catStaging = Join-Path $staging "MicLinkVirtualAudio.cat"
if (-not (Test-Path $catStaging)) {
    Write-Error "Inf2Cat did not create MicLinkVirtualAudio.cat"
}

$cat = Join-Path $DriverDir "MicLinkVirtualAudio.cat"
Copy-Item $catStaging -Destination $cat -Force

Write-Host "Signing MicLinkVirtualAudio.cat..." -ForegroundColor Cyan
& $signtool sign /v /fd SHA256 /f $pfxPath /p $pfxPassword /tr http://timestamp.digicert.com /td SHA256 $cat
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$installDir = Join-Path $PSScriptRoot "..\MicLinkVirtualAudio\install"
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item $sys, $inf, $cat -Destination $installDir -Force

Write-Host ""
Write-Host "OK: Signed driver package in $DriverDir" -ForegroundColor Green
Write-Host 'Files: MicLinkVirtualAudio.sys, MicLinkVirtualAudio.inf, MicLinkVirtualAudio.cat'
Write-Host 'Next: pnputil /add-driver ... /install  (Admin), then rebuild MicLink (F5).'
