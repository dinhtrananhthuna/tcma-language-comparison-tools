#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Interactive script to create conventional commit messages

.DESCRIPTION
    This script helps create properly formatted commit messages following
    the Conventional Commits specification for the TCMA Language Comparison Tool.

.EXAMPLE
    .\create-commit.ps1
#>

param(
    [string]$Type,
    [string]$Scope,
    [string]$Description,
    [string]$Body,
    [string]$BreakingChange,
    [string]$IssueNumber,
    [switch]$Help
)

if ($Help) {
    Write-Host @"
TCMA Language Comparison Tool - Commit Message Helper

Usage:
    .\create-commit.ps1 [Options]

Options:
    -Type <string>           Commit type (feat, fix, docs, style, refactor, perf, test, chore, ci, build, revert)
    -Scope <string>          Commit scope (core, gui, api, config, test, docs, build)
    -Description <string>    Commit description
    -Body <string>           Commit body (optional)
    -BreakingChange <string> Breaking change description (optional)
    -IssueNumber <string>    Issue number to reference (optional)
    -Help                    Show this help message

Examples:
    .\create-commit.ps1 -Type feat -Scope core -Description "add semantic similarity matching"
    .\create-commit.ps1 -Type fix -Scope gui -Description "resolve memory leak" -IssueNumber "123"
    .\create-commit.ps1 -Type feat -Scope core -Description "change threshold" -BreakingChange "Default threshold changed from 0.35 to 0.4"

Interactive Mode:
    Run without parameters for interactive mode
"@
    exit 0
}

# Commit types
$commitTypes = @(
    "feat",
    "fix", 
    "docs",
    "style",
    "refactor",
    "perf",
    "test",
    "chore",
    "ci",
    "build",
    "revert"
)

# Scopes
$scopes = @(
    "core",
    "gui",
    "api", 
    "config",
    "test",
    "docs",
    "build"
)

function Show-Menu {
    param([string]$Title, [array]$Options, [string]$Default = "")
    
    Write-Host "`n$Title" -ForegroundColor Cyan
    for ($i = 0; $i -lt $Options.Count; $i++) {
        $marker = if ($Options[$i] -eq $Default) { ">" } else { " " }
        Write-Host "  $marker $($i + 1). $($Options[$i])" -ForegroundColor $(if ($Options[$i] -eq $Default) { "Green" } else { "White" })
    }
    Write-Host "  > 0. Custom input" -ForegroundColor Yellow
}

function Get-UserChoice {
    param([array]$Options, [string]$Prompt, [string]$Default = "")
    
    do {
        Show-Menu -Title $Prompt -Options $Options -Default $Default
        $choice = Read-Host "`nSelect option (0-$($Options.Count))"
        
        if ($choice -eq "0") {
            return Read-Host "Enter custom value"
        }
        elseif ($choice -match "^\d+$" -and [int]$choice -ge 1 -and [int]$choice -le $Options.Count) {
            return $Options[[int]$choice - 1]
        }
        else {
            Write-Host "Invalid choice. Please try again." -ForegroundColor Red
        }
    } while ($true)
}

# Interactive mode if no parameters provided
if (-not $Type -and -not $Scope -and -not $Description) {
    Write-Host "TCMA Language Comparison Tool - Commit Message Helper" -ForegroundColor Green
    Write-Host "=====================================================" -ForegroundColor Green
    
    $Type = Get-UserChoice -Options $commitTypes -Prompt "Select commit type:" -Default "feat"
    $Scope = Get-UserChoice -Options $scopes -Prompt "Select scope (optional):" -Default ""
    
    if ($Scope -eq "") {
        $Scope = Read-Host "Enter custom scope (or press Enter to skip)"
    }
    
    $Description = Read-Host "Enter commit description"
    $Body = Read-Host "Enter commit body (optional, press Enter to skip)"
    $BreakingChange = Read-Host "Enter breaking change description (optional, press Enter to skip)"
    $IssueNumber = Read-Host "Enter issue number (optional, press Enter to skip)"
}

# Validate required parameters
if (-not $Type) {
    Write-Host "Error: Commit type is required" -ForegroundColor Red
    exit 1
}

if (-not $Description) {
    Write-Host "Error: Commit description is required" -ForegroundColor Red
    exit 1
}

# Build commit message
$commitMessage = $Type

if ($Scope) {
    $commitMessage += "($Scope)"
}

if ($BreakingChange) {
    $commitMessage += "!"
}

$commitMessage += ": $Description"

if ($Body) {
    $commitMessage += "`n`n$Body"
}

if ($BreakingChange) {
    $commitMessage += "`n`nBREAKING CHANGE: $BreakingChange"
}

if ($IssueNumber) {
    $commitMessage += "`n`nCloses #$IssueNumber"
}

# Display the commit message
Write-Host "`nGenerated commit message:" -ForegroundColor Green
Write-Host "=========================" -ForegroundColor Green
Write-Host $commitMessage -ForegroundColor Yellow

# Ask for confirmation
$confirm = Read-Host "`nProceed with commit? (y/N)"
if ($confirm -eq "y" -or $confirm -eq "Y") {
    git commit -m $commitMessage
    Write-Host "Commit created successfully!" -ForegroundColor Green
}
else {
    Write-Host "Commit cancelled." -ForegroundColor Yellow
} 