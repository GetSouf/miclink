# Builds MicLinkVirtualAudio.inf for unsigned dev install (no .cat, PnpLockDown off).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$srcInf = Join-Path $root "MicLinkVirtualAudio\upstream\x64\Release\package\VirtualAudioDriver.inf"
if (-not (Test-Path $srcInf)) {
    $srcInf = Get-ChildItem -Path (Join-Path $root "MicLinkVirtualAudio\upstream") -Recurse -Filter "VirtualAudioDriver.inf" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $srcInf) {
    Write-Error "VirtualAudioDriver.inf not found. Run build-miclink-driver.bat first."
}

$text = Get-Content $srcInf -Raw
$text = $text -replace "(?m)^CatalogFile\s*=.*\r?\n", ""
$text = $text -replace "PnpLockDown\s*=\s*1", "PnpLockDown = 0"
$text = $text -replace "virtualaudiodriver\.sys", "MicLinkVirtualAudio.sys"
$text = $text -replace "(?ms)\[SignatureAttributes\].*?\[SignatureAttributes\.PETrust\]\r?\nPETrust=true\r?\n\r?\n", ""

$defaultInstall = @"

[DefaultInstall.NTamd64]
OptionDesc=%VIRTUALAUDIODRIVER_SA.DeviceDesc%
CopyFiles=VIRTUALAUDIODRIVER_SA.CopyList
AddReg=VIRTUALAUDIODRIVER_SA.AddReg

[DefaultInstall.NTamd64.HW]
AddReg=AUDIOHW.AddReg

[DefaultInstall.NTamd64.Services]
Include=ks.inf,wdmaudio.inf
Needs=KS.Registration, WDMAUDIO.Registration
AddService=VirtualAudioDriver,0x00000002,VirtualAudioDriver_Service_Inst

[DefaultInstall.NTamd64.Interfaces]
AddInterface=%KSCATEGORY_AUDIO%, %KSNAME_WaveSpeaker%, VIRTUALAUDIODRIVER.I.WaveSpeaker
AddInterface=%KSCATEGORY_RENDER%, %KSNAME_WaveSpeaker%, VIRTUALAUDIODRIVER.I.WaveSpeaker
AddInterface=%KSCATEGORY_REALTIME%, %KSNAME_WaveSpeaker%, VIRTUALAUDIODRIVER.I.WaveSpeaker
AddInterface=%KSCATEGORY_AUDIO%, %KSNAME_TopologySpeaker%, VIRTUALAUDIODRIVER.I.TopologySpeaker
AddInterface=%KSCATEGORY_TOPOLOGY%, %KSNAME_TopologySpeaker%, VIRTUALAUDIODRIVER.I.TopologySpeaker
AddInterface=%KSCATEGORY_AUDIO%,    %KSNAME_WaveMicArray1%, VIRTUALAUDIODRIVER.I.WaveMicArray1
AddInterface=%KSCATEGORY_REALTIME%, %KSNAME_WaveMicArray1%, VIRTUALAUDIODRIVER.I.WaveMicArray1
AddInterface=%KSCATEGORY_CAPTURE%,  %KSNAME_WaveMicArray1%, VIRTUALAUDIODRIVER.I.WaveMicArray1
AddInterface=%KSCATEGORY_AUDIO%,    %KSNAME_TopologyMicArray1%, VIRTUALAUDIODRIVER.I.TopologyMicArray1
AddInterface=%KSCATEGORY_TOPOLOGY%, %KSNAME_TopologyMicArray1%, VIRTUALAUDIODRIVER.I.TopologyMicArray1

[DefaultInstall.NTamd64.Wdf]
KmdfService = VirtualAudioDriver, VIRTUALAUDIODRIVER_SA_WdfSect

"@

if ($text -notmatch "\[DefaultInstall\.NTamd64\]") {
    $text = $text + $defaultInstall
}

$destDirs = @(
    (Join-Path $root "..\Pc\MicLinkWinUI\MicLinkWinUI\Assets\Driver"),
    (Join-Path $root "MicLinkVirtualAudio\install")
)
foreach ($dest in $destDirs) {
    $dest = [System.IO.Path]::GetFullPath($dest)
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Set-Content -Path (Join-Path $dest "MicLinkVirtualAudio.inf") -Value $text -Encoding Unicode
}

Write-Host "Prepared MicLinkVirtualAudio.inf in Assets/Driver and install/"
