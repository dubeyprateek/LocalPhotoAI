# publish.ps1 — Builds a self-contained single-file executable for LocalPhotoAI
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputDir = "publish"
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing LocalPhotoAI ($Runtime, $Configuration)..." -ForegroundColor Cyan

dotnet publish src/LocalPhotoAI.Host/LocalPhotoAI.Host.csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Published to: $OutputDir" -ForegroundColor Green
Write-Host ""

$exe = Get-ChildItem "$OutputDir/LocalPhotoAI.Host*" -Filter "*.exe" | Select-Object -First 1
if ($exe) {
    Write-Host "Executable: $($exe.FullName)" -ForegroundColor Green
    Write-Host "Size: $([math]::Round($exe.Length / 1MB, 1)) MB" -ForegroundColor Green
}

Write-Host ""
Write-Host "To run: ./$OutputDir/$($exe.Name)" -ForegroundColor Yellow
