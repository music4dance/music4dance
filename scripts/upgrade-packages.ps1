<#
.SYNOPSIS
    Upgrades NuGet and Yarn packages for the music4dance solution.

.DESCRIPTION
    Upgrades both NuGet (.NET) and Yarn (client) packages.
    - minor mode: upgrades to latest without crossing major version boundaries (safe, no breaking changes)
    - major mode: upgrades everything to absolute latest, including major version bumps

.PARAMETER Mode
    'minor' (default): stay within current major version for all packages.
    'major': upgrade to absolute latest, crossing major version boundaries.

.PARAMETER SkipNuGet
    Skip NuGet package upgrades.

.PARAMETER SkipYarn
    Skip Yarn package upgrades.

.EXAMPLE
    .\upgrade-packages.ps1
    .\upgrade-packages.ps1 -Mode major
    .\upgrade-packages.ps1 -Mode minor -SkipYarn
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet("minor", "major")]
    [string]$Mode = "minor",

    [switch]$SkipNuGet,
    [switch]$SkipYarn
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot   = Split-Path -Parent $ScriptDir
$ClientApp  = Join-Path $RepoRoot "m4d\ClientApp"

$ModeLabel = if ($Mode -eq "minor") { "minor/patch only (no major version changes)" } else { "all versions including major bumps" }
Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Package upgrade mode: $Mode" -ForegroundColor Cyan
Write-Host "  ($ModeLabel)" -ForegroundColor DarkCyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# ---------------------------------------------------------------------------
# NuGet
# ---------------------------------------------------------------------------
if (-not $SkipNuGet) {
    Write-Host "--- NuGet packages ---" -ForegroundColor Yellow

    # Ensure dotnet-outdated-tool is available
    $toolInstalled = dotnet tool list -g 2>$null | Select-String "dotnet-outdated-tool"
    if (-not $toolInstalled) {
        Write-Host "Installing dotnet-outdated-tool globally..." -ForegroundColor DarkYellow
        dotnet tool install -g dotnet-outdated-tool
    }

    Push-Location $RepoRoot
    try {
        if ($Mode -eq "minor") {
            # --version-lock Major: allow minor and patch updates, never cross major version
            Write-Host "Running: dotnet outdated --upgrade --version-lock Major" -ForegroundColor DarkGray
            dotnet outdated --upgrade --version-lock Major
        } else {
            # No version lock: upgrade to absolute latest, crossing major versions
            Write-Host "Running: dotnet outdated --upgrade" -ForegroundColor DarkGray
            dotnet outdated --upgrade
        }
    } finally {
        Pop-Location
    }

    Write-Host ""
} else {
    Write-Host "--- NuGet packages: skipped ---" -ForegroundColor DarkGray
    Write-Host ""
}

# ---------------------------------------------------------------------------
# Yarn (v4 Berry)
# ---------------------------------------------------------------------------
if (-not $SkipYarn) {
    Write-Host "--- Yarn packages (v4 Berry) ---" -ForegroundColor Yellow

    Push-Location $ClientApp
    try {
        if ($Mode -eq "minor") {
            # Upgrade all packages within their current semver range.
            # Since package.json uses '^' (compatible-with) ranges, this stays within the same major version.
            Write-Host "Running: yarn up '*' '@*/*'" -ForegroundColor DarkGray
            yarn up '*' '@*/*'
        } else {
            # Yarn v4 has no --latest flag. Use npm-check-updates (via yarn dlx) to rewrite
            # package.json ranges to the absolute latest versions, then install.
            Write-Host "Running: yarn dlx npm-check-updates --upgrade (rewrites package.json ranges)" -ForegroundColor DarkGray
            yarn dlx npm-check-updates --upgrade
            Write-Host "Running: yarn install (installs rewritten ranges)" -ForegroundColor DarkGray
            yarn install
        }
    } finally {
        Pop-Location
    }

    Write-Host ""
} else {
    Write-Host "--- Yarn packages: skipped ---" -ForegroundColor DarkGray
    Write-Host ""
}

Write-Host "==================================================" -ForegroundColor Green
Write-Host "  Done. Review changes before committing." -ForegroundColor Green
if ($Mode -eq "major") {
    Write-Host "  MAJOR mode: run tests before committing!" -ForegroundColor Red
}
Write-Host "==================================================" -ForegroundColor Green
Write-Host ""
