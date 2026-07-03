param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$project = Join-Path $root 'Pc\MicLinkWinUI\MicLinkWinUI\MicLinkWinUI.csproj'

Write-Host "Publishing MicLink ($Configuration, win-x64, self-contained)..."

dotnet publish $project `
    -c $Configuration `
    -p:Platform=x64 `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:WindowsAppSDKSelfContained=true

$publishDir = Join-Path $root "Pc\MicLinkWinUI\MicLinkWinUI\bin\$Configuration\net8.0-windows10.0.19041.0\win-x64\publish"
Write-Host ""
Write-Host "OK: $publishDir"
Write-Host "Zip this folder for portable distribution."
