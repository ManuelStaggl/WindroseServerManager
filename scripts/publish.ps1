# Windrose Server Manager — Self-Contained Single-File Publish (Avalonia Desktop)
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $root "artifacts\publish" }

Write-Host ""
Write-Host "Windrose Server Manager — Publish" -ForegroundColor Cyan
Write-Host "  Configuration: $Configuration"
Write-Host "  Runtime:       $Runtime"
Write-Host "  Output:        $OutputDir"
Write-Host ""

if (Test-Path $OutputDir) {
    Write-Host "Cleaning $OutputDir ..." -ForegroundColor DarkGray
    Remove-Item $OutputDir -Recurse -Force
}

$project = Join-Path $root "src\WindroseServerManager.App\WindroseServerManager.App.csproj"

dotnet publish $project `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishReadyToRun=true `
    --output $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed." -ForegroundColor Red
    exit 1
}

$exe = Join-Path $OutputDir "WindroseServerManager.exe"
if (Test-Path $exe) {
    $size = (Get-Item $exe).Length / 1MB
    Write-Host ""
    Write-Host ("OK: {0} ({1:N1} MB)" -f $exe, $size) -ForegroundColor Green
} else {
    Write-Warning "Expected EXE nicht gefunden unter $exe"
}
