# MSIX Packaging Script for TCMA Language Comparison Tool
# This creates a modern Windows installer package

param(
    [string]$Version = "1.0.0"
)

$AppName = "Tcma.LanguageComparison.Gui"
$PublishDir = "publish"
$MSIXDir = "msix-package"

Write-Host "=== MSIX Packaging for TCMA Language Comparison Tool ===" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Yellow

# 1. Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Green
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
if (Test-Path $MSIXDir) { Remove-Item $MSIXDir -Recurse -Force }
if (Test-Path "*.msix") { Remove-Item "*.msix" -Force }

# 2. Build and publish the application
Write-Host "Building and publishing application..." -ForegroundColor Green
dotnet publish "$AppName.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $PublishDir

if (-not (Test-Path "$PublishDir\$AppName.exe")) {
    Write-Error "Build failed - executable not found"
    exit 1
}

# 3. Remove Core.exe to avoid confusion
$CoreExe = "$PublishDir\Tcma.LanguageComparison.Core.exe"
if (Test-Path $CoreExe) {
    Write-Host "Removing Core.exe..." -ForegroundColor Yellow
    Remove-Item $CoreExe -Force
}

# 4. Create MSIX package using dotnet
Write-Host "Creating MSIX package..." -ForegroundColor Green

# Update project file temporarily for MSIX packaging
$ProjectFile = "$AppName.csproj"
$OriginalContent = Get-Content $ProjectFile -Raw

# Add MSIX properties to project file
$MSIXContent = $OriginalContent -replace '</PropertyGroup>', @"
    <WindowsPackageType>MSIX</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <PublishProfile>win-x64</PublishProfile>
    <GenerateAppInstallerFile>true</GenerateAppInstallerFile>
    <AppxPackageSigningEnabled>false</AppxPackageSigningEnabled>
  </PropertyGroup>
"@

Set-Content $ProjectFile $MSIXContent

try {
    # Create MSIX package
    dotnet publish $ProjectFile -c Release -r win-x64 --self-contained true -p:GenerateAppxPackageOnBuild=true -p:AppxPackageDir="$MSIXDir\" -o $PublishDir
    
    # Look for generated MSIX file
    $MSIXFile = Get-ChildItem -Path $MSIXDir -Filter "*.msix" -Recurse | Select-Object -First 1
    
    if ($MSIXFile) {
        Copy-Item $MSIXFile.FullName -Destination ".\$AppName-$Version.msix"
        Write-Host "✅ MSIX package created: $AppName-$Version.msix" -ForegroundColor Green
        Write-Host "📦 Package size: $([math]::Round((Get-Item ".\$AppName-$Version.msix").Length / 1MB, 2)) MB" -ForegroundColor Cyan
    } else {
        throw "MSIX file not generated"
    }
}
catch {
    Write-Warning "MSIX generation failed: $_"
    Write-Host "Falling back to manual MSIX creation..." -ForegroundColor Yellow
    
    # Manual MSIX creation using MakeAppx (requires Windows SDK)
    if (Get-Command "makeappx.exe" -ErrorAction SilentlyContinue) {
        # Create manifest and package manually
        Write-Host "Using MakeAppx tool..." -ForegroundColor Yellow
        # This would require creating AppxManifest.xml and using makeappx
        Write-Warning "Manual MSIX creation not implemented. Use ZIP distribution instead."
    } else {
        Write-Warning "Windows SDK (MakeAppx) not found. Cannot create MSIX package."
    }
}
finally {
    # Restore original project file
    Set-Content $ProjectFile $OriginalContent
}

Write-Host "=== MSIX Packaging Complete ===" -ForegroundColor Cyan
Write-Host "To install: Right-click the .msix file and select 'Install'" -ForegroundColor Yellow