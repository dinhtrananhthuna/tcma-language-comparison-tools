# MSIX Packaging Script for TCMA Language Comparison Tool
# This creates a modern Windows installer package using MakeAppx

param(
    [string]$Version = "1.0.0"
)

$AppName = "TcmaLanguageComparison"
$AppDisplayName = "TCMA Language Comparison Tool"
$PublisherName = "TCMA Team"
$PublishDir = "publish"
$MSIXDir = "msix-build"
$ExeName = "Tcma.LanguageComparison.Gui.exe"

Write-Host "=== MSIX Packaging for TCMA Language Comparison Tool ===" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Yellow

# 1. Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Green
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
if (Test-Path $MSIXDir) { Remove-Item $MSIXDir -Recurse -Force }
if (Test-Path "*.msix") { Remove-Item "*.msix" -Force }

# 2. Build and publish the application
Write-Host "Building and publishing application..." -ForegroundColor Green
dotnet publish "Tcma.LanguageComparison.Gui.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $PublishDir

if (-not (Test-Path "$PublishDir\$ExeName")) {
    Write-Error "Build failed - executable not found at $PublishDir\$ExeName"
    exit 1
}

# 3. Remove Core.exe to avoid confusion
$CoreExe = "$PublishDir\Tcma.LanguageComparison.Core.exe"
if (Test-Path $CoreExe) {
    Write-Host "Removing Core.exe..." -ForegroundColor Yellow
    Remove-Item $CoreExe -Force
}

# 4. Check for MakeAppx tool
Write-Host "Checking for Windows SDK MakeAppx tool..." -ForegroundColor Green
$MakeAppxPaths = @(
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe",
    "${env:ProgramFiles}\Windows Kits\10\bin\*\x64\makeappx.exe"
)

$MakeAppx = $null
foreach ($pattern in $MakeAppxPaths) {
    $found = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
    if ($found) {
        $MakeAppx = $found.FullName
        break
    }
}

if (-not $MakeAppx) {
    Write-Error "MakeAppx.exe not found. Please install Windows SDK from:"
    Write-Host "https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/" -ForegroundColor Yellow
    Write-Host "Alternative: Use build-zip.ps1 for portable distribution" -ForegroundColor Cyan
    exit 1
}

Write-Host "Found MakeAppx: $MakeAppx" -ForegroundColor Green

# 5. Create MSIX build directory and copy files
Write-Host "Preparing MSIX package structure..." -ForegroundColor Green
New-Item -ItemType Directory -Path $MSIXDir -Force | Out-Null
Copy-Item -Path "$PublishDir\*" -Destination $MSIXDir -Recurse -Force

# 6. Create AppxManifest.xml
Write-Host "Creating AppxManifest.xml..." -ForegroundColor Green
$ManifestContent = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">
  <Identity Name="$AppName"
            Publisher="CN=$PublisherName"
            Version="$Version.0" />
  
  <mp:PhoneIdentity PhoneProductId="$(New-Guid)" PhonePublisherId="00000000-0000-0000-0000-000000000000"/>
  
  <Properties>
    <DisplayName>$AppDisplayName</DisplayName>
    <PublisherDisplayName>$PublisherName</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
    <Description>AI-powered tool for comparing and aligning localization content between different languages using Google Gemini embeddings</Description>
  </Properties>
  
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>
  
  <Capabilities>
    <Capability Name="internetClient" />
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
  
  <Applications>
    <Application Id="App" Executable="$ExeName" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="$AppDisplayName"
                          Description="$AppDisplayName"
                          BackgroundColor="transparent"
                          Square150x150Logo="Assets\Square150x150Logo.png"
                          Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" />
      </uap:VisualElements>
    </Application>
  </Applications>
</Package>
"@

Set-Content -Path "$MSIXDir\AppxManifest.xml" -Value $ManifestContent -Encoding UTF8

# 7. Create placeholder assets (required by MSIX)
Write-Host "Creating placeholder assets..." -ForegroundColor Green
$AssetsDir = "$MSIXDir\Assets"
New-Item -ItemType Directory -Path $AssetsDir -Force | Out-Null

# Create simple placeholder PNG files (minimal 1x1 pixel PNG)
$PlaceholderPNG = @(
    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
    0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
    0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
    0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
)

$AssetFiles = @("StoreLogo.png", "Square150x150Logo.png", "Square44x44Logo.png", "Wide310x150Logo.png")
foreach ($asset in $AssetFiles) {
    [System.IO.File]::WriteAllBytes("$AssetsDir\$asset", $PlaceholderPNG)
}

# 8. Create MSIX package using MakeAppx
Write-Host "Creating MSIX package with MakeAppx..." -ForegroundColor Green
$MSIXFile = "$AppName-$Version.msix"

try {
    & $MakeAppx pack /d $MSIXDir /p $MSIXFile /overwrite
    
    if (Test-Path $MSIXFile) {
        $MSIXSize = [math]::Round((Get-Item $MSIXFile).Length / 1MB, 2)
        Write-Host "✅ MSIX package created successfully!" -ForegroundColor Green
        Write-Host "📦 File: $MSIXFile" -ForegroundColor Cyan
        Write-Host "📦 Size: $MSIXSize MB" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "=== Installation Instructions ===" -ForegroundColor Yellow
        Write-Host "1. Right-click on $MSIXFile" -ForegroundColor White
        Write-Host "2. Select 'Install'" -ForegroundColor White
        Write-Host "3. Or double-click to install via App Installer" -ForegroundColor White
        Write-Host ""
        Write-Host "Note: You may need to enable 'Developer mode' in Windows Settings" -ForegroundColor Yellow
        Write-Host "or install the certificate if Windows blocks the installation." -ForegroundColor Yellow
    } else {
        throw "MSIX file was not created"
    }
}
catch {
    Write-Error "Failed to create MSIX package: $_"
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "1. Ensure Windows SDK is properly installed" -ForegroundColor White
    Write-Host "2. Try running as Administrator" -ForegroundColor White
    Write-Host "3. Use build-zip.ps1 as alternative" -ForegroundColor White
    exit 1
}

Write-Host "=== MSIX Packaging Complete ===" -ForegroundColor Cyan