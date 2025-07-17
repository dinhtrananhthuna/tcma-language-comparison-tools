# Inno Setup Builder for TCMA Language Comparison Tool
# Requires Inno Setup to be installed: https://jrsoftware.org/isinfo.php

param(
    [string]$Version = "1.0.0"
)

$AppName = "Tcma.LanguageComparison.Gui"
$PublishDir = "publish"
$InnoScript = "installer.iss"

Write-Host "=== Inno Setup Installer for TCMA Language Comparison Tool ===" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Yellow

# 1. Check if Inno Setup is installed
$InnoSetupPaths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 5\ISCC.exe"
)

$ISCC = $null
foreach ($path in $InnoSetupPaths) {
    if (Test-Path $path) {
        $ISCC = $path
        break
    }
}

if (-not $ISCC) {
    Write-Error "Inno Setup not found. Please install from: https://jrsoftware.org/isinfo.php"
    Write-Host "After installation, run this script again." -ForegroundColor Yellow
    exit 1
}

Write-Host "Found Inno Setup: $ISCC" -ForegroundColor Green

# 2. Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Green
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
if (Test-Path "installer-output") { Remove-Item "installer-output" -Recurse -Force }

# 3. Build and publish the application
Write-Host "Building and publishing application..." -ForegroundColor Green
dotnet publish "$AppName.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $PublishDir

if (-not (Test-Path "$PublishDir\$AppName.exe")) {
    Write-Error "Build failed - executable not found"
    exit 1
}

# 4. Remove Core.exe to avoid confusion
$CoreExe = "$PublishDir\Tcma.LanguageComparison.Core.exe"
if (Test-Path $CoreExe) {
    Write-Host "Removing Core.exe..." -ForegroundColor Yellow
    Remove-Item $CoreExe -Force
}

# 5. Create minimal LICENSE.txt if not exists
if (-not (Test-Path "LICENSE.txt")) {
    Write-Host "Creating LICENSE.txt..." -ForegroundColor Yellow
    Set-Content "LICENSE.txt" @"
TCMA Language Comparison Tool

Copyright (c) $(Get-Date -Format yyyy) TCMA Team

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
"@
}

# 6. Update version in Inno Setup script
if (Test-Path $InnoScript) {
    Write-Host "Updating version in Inno Setup script..." -ForegroundColor Green
    $InnoContent = Get-Content $InnoScript -Raw
    $InnoContent = $InnoContent -replace '#define MyAppVersion ".*"', "#define MyAppVersion `"$Version`""
    Set-Content $InnoScript $InnoContent
}

# 7. Build installer
Write-Host "Building installer with Inno Setup..." -ForegroundColor Green
try {
    & $ISCC $InnoScript
    
    # Check if installer was created
    $InstallerFiles = Get-ChildItem -Path "installer-output" -Filter "*.exe" -ErrorAction SilentlyContinue
    
    if ($InstallerFiles) {
        $InstallerFile = $InstallerFiles[0]
        Write-Host "✅ Installer created successfully!" -ForegroundColor Green
        Write-Host "📦 File: $($InstallerFile.FullName)" -ForegroundColor Cyan
        Write-Host "📦 Size: $([math]::Round($InstallerFile.Length / 1MB, 2)) MB" -ForegroundColor Cyan
    } else {
        Write-Error "Installer file not found in output directory"
    }
}
catch {
    Write-Error "Failed to build installer: $_"
    exit 1
}

Write-Host "`n=== Inno Setup Installer Complete ===" -ForegroundColor Cyan
Write-Host "Installer ready in: installer-output\" -ForegroundColor Green