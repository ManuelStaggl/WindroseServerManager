# Windrose Server Manager - Release Build
# - Clean + Publish (self-contained, single-file)
# - ZIP Output
# - Inno-Setup Installer (wenn verfuegbar)
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\WindroseServerManager.App\WindroseServerManager.App.csproj"
$publishDir = Join-Path $root "artifacts\publish"
$artifactsDir = Join-Path $root "artifacts"

Write-Host ""
Write-Host "Windrose Server Manager - Release Build" -ForegroundColor Cyan
Write-Host ""

# Version aus csproj
[xml]$csproj = Get-Content $project
$version = $csproj.Project.PropertyGroup.Version | Select-Object -First 1
if (-not $version) {
    Write-Host "Konnte Version nicht lesen - fallback 1.0.0" -ForegroundColor Yellow
    $version = "1.0.0"
}
Write-Host "  Version: $version"
Write-Host "  Runtime: $Runtime"
Write-Host ""

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (-not (Test-Path $artifactsDir)) { New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null }

Write-Host "Publish..." -ForegroundColor DarkGray
dotnet publish $project `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishReadyToRun=true `
    --output $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish fehlgeschlagen." -ForegroundColor Red
    exit 1
}

$zipPath = Join-Path $artifactsDir "WindroseServerManager-$version-portable.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Write-Host "ZIP: $zipPath" -ForegroundColor DarkGray
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Host ("OK: {0} ({1:N1} MB)" -f $zipPath, $zipSize) -ForegroundColor Green

$iscc = $null
$commonPaths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe"
)
foreach ($p in $commonPaths) {
    if (Test-Path $p) { $iscc = $p; break }
}
if (-not $iscc) {
    $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
}

if ($iscc) {
    Write-Host ""
    Write-Host "Inno-Setup: $iscc" -ForegroundColor DarkGray
    $installerScript = Join-Path $PSScriptRoot "installer.iss"
    & $iscc "/DMyAppVersion=$version" $installerScript
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Installer OK." -ForegroundColor Green
    } else {
        Write-Host "Installer fehlgeschlagen." -ForegroundColor Yellow
    }
} else {
    Write-Host ""
    Write-Host "Inno-Setup nicht gefunden - skip Installer." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Fertig." -ForegroundColor Cyan
