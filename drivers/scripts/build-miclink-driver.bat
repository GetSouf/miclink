@echo off
setlocal
set "VCTOOLS=C:\Users\kompik\Documents\study\Microfon Application\drivers\MicLinkVirtualAudio\msvc-vctools"
set "BASE=C:\Users\kompik\Documents\study\Microfon Application\drivers\MicLinkVirtualAudio\msvc-overlay\v145"
set "SLN=%~dp0..\MicLinkVirtualAudio\upstream\VirtualAudioDriver.sln"
set "MSBUILD=C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
set "SRC_VCTOOLS=C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.50.35717"

if not exist "%SRC_VCTOOLS%\bin\Hostx64\x64\cl.exe" (
    echo.
    echo C++ compiler not found. In Visual Studio Installer ^(run as Administrator^):
    echo   Add workload: Desktop development with C++
    exit /b 1
)

if not exist "%VCTOOLS%\bin\Hostx64\x64\cl.exe" (
    echo Preparing compiler tools...
    robocopy "%SRC_VCTOOLS%" "%VCTOOLS%" /E /NFL /NDL /NJH /NJS /nc /ns /np >nul
)

set "PATH=%VCTOOLS%\bin\Hostx64\x64;%PATH%"

"%MSBUILD%" "%SLN%" /p:Configuration=Release /p:Platform=x64 "/p:VCToolsInstallDir=%VCTOOLS%" "/p:V145PropsFile=%BASE%\Toolset.props" "/p:V145TargetsFile=%BASE%\Toolset.targets" "/p:V143PropsFile=%BASE%\Toolset.props" "/p:V143TargetsFile=%BASE%\Toolset.targets" /p:SpectreMitigation=false /p:Driver_SpectreMitigation=false /p:SkipPackageVerification=true /p:ApiValidator_Enable=false /p:UseInfVerifierEx=false /p:ValidateDrivers=false /p:InfVerif_Enable=false /p:DisableVerification=true /p:SignMode=Off /p:EnableInf2cat=false /m
if errorlevel 1 exit /b 1

set "SYS="
for /r "%~dp0..\MicLinkVirtualAudio\upstream" %%F in (MicLinkVirtualAudio.sys) do set "SYS=%%F"
if not defined SYS (
    echo MicLinkVirtualAudio.sys not found after build
    exit /b 1
)

set "DEST=%~dp0..\..\Pc\MicLinkWinUI\MicLinkWinUI\Assets\Driver"
set "INSTALL=%~dp0..\MicLinkVirtualAudio\install"
if not exist "%DEST%" mkdir "%DEST%"
if not exist "%INSTALL%" mkdir "%INSTALL%"
copy /y "%SYS%" "%DEST%\MicLinkVirtualAudio.sys" >nul
copy /y "%SYS%" "%INSTALL%\MicLinkVirtualAudio.sys" >nul
powershell -ExecutionPolicy Bypass -File "%~dp0prepare-driver-package.ps1"
if errorlevel 1 exit /b 1
echo.
echo OK: %SYS%
echo Copied to Assets\Driver
exit /b 0
