# Build & Zip script for TCMA Language Comparison Tool (simple version)
# Chỉ build bản release và đóng gói zip vào thư mục release, không tạo README

param(
    [string]$Version = "1.0.0"
)

$AppName = "Tcma.LanguageComparison.Gui"
$PublishDir = "publish"
$ReleaseDir = "release"
$ZipName = "$AppName-$Version-portable.zip"
$ZipPath = Join-Path $ReleaseDir $ZipName

Write-Host "=== Build & Zip TCMA Language Comparison Tool ===" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Yellow

# 1. Xóa build cũ
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

# 2. Build release self-contained
Write-Host "Building release..." -ForegroundColor Green
dotnet publish "$AppName.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $PublishDir

if (-not (Test-Path "$PublishDir\$AppName.exe")) {
    Write-Error "Build failed - executable not found"
    exit 1
}

# 3. Tạo thư mục release nếu chưa có
if (-not (Test-Path $ReleaseDir)) { New-Item -ItemType Directory -Path $ReleaseDir | Out-Null }

# 4. Đóng gói zip vào thư mục release
Write-Host "Zipping..." -ForegroundColor Green
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal

if (Test-Path $ZipPath) {
    $ZipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
    Write-Host "✅ ZIP created: $ZipPath ($ZipSize MB)" -ForegroundColor Green
} else {
    Write-Error "Failed to create ZIP package"
    exit 1
}