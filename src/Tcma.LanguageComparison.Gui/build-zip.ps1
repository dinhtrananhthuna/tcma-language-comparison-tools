# Build & Zip script for TCMA Language Comparison Tool (Windows-friendly)
param(
    [string]$Version = "1.0.0",
    [switch]$CreateRelease,
    [string]$ReleaseName = ""
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

# 5. Tạo GitHub Release nếu được yêu cầu
if ($CreateRelease) {
    Write-Host "Creating GitHub Release..." -ForegroundColor Cyan

    # Kiểm tra GitHub CLI
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Error "GitHub CLI (gh) is not installed. Please install it first: https://cli.github.com/"
        exit 1
    }

    # Kiểm tra authentication
    gh auth status | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Not authenticated with GitHub. Run 'gh auth login' first."
        exit 1
    }

    # Lấy thông tin repo (owner/name)
    $repoInfo = gh repo view --json owner,name | ConvertFrom-Json
    $repoFullName = "$($repoInfo.owner.login)/$($repoInfo.name)"

    # Tạo release name nếu chưa có
    if ([string]::IsNullOrEmpty($ReleaseName)) {
        $ReleaseName = "TCMA Language Comparison Tool v$Version"
    }
    $TagName = "v$Version"

    # Tạo release body
    $ReleaseBody = @"
## TCMA Language Comparison Tool v$Version

- Build Time: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
- Package Size: $ZipSize MB
"@

    # Tạo release (nếu tag đã tồn tại sẽ báo lỗi)
    $releaseResult = gh release create $TagName $ZipPath --title "$ReleaseName" --notes "$ReleaseBody" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ GitHub Release created successfully!" -ForegroundColor Green
        Write-Host "Release URL: https://github.com/$repoFullName/releases/tag/$TagName" -ForegroundColor Yellow
    } else {
        Write-Error "Failed to create GitHub release: $releaseResult"
        exit 1
    }
}

Write-Host "Build completed successfully!" -ForegroundColor Green