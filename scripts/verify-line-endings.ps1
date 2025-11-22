<#
.SYNOPSIS
    Verifies that all text files have consistent line endings and no marker bytes.
.DESCRIPTION
    This script checks all text files for line ending consistency and the presence
    of corrupt 0x1A marker bytes. Can be used in CI to prevent regressions.
    
    Validates:
    - .yml, .yaml files: Should use LF
    - ClientApp files: Should use LF
    - Other files: Should use CRLF
.PARAMETER FailOnIssues
    Exit with non-zero code if issues are found (useful for CI).
#>
[CmdletBinding()]
param(
    [switch]$FailOnIssues
)

$ErrorActionPreference = "Stop"

# Find repository root (look for .git directory)
$scriptDir = $PSScriptRoot
$repoRoot = $scriptDir
while ($repoRoot -and -not (Test-Path (Join-Path $repoRoot ".git"))) {
    $parent = Split-Path $repoRoot -Parent
    if ($parent -eq $repoRoot) {
        # We've reached the root of the drive
        $repoRoot = $null
        break
    }
    $repoRoot = $parent
}

if (-not $repoRoot) {
    Write-Error "Could not find repository root (no .git directory found)"
    exit 1
}

# File extensions to check
$textExtensions = @(
    '*.cs', '*.cshtml', '*.csproj', '*.sln', '*.config',
    '*.json', '*.xml', '*.yml', '*.yaml', '*.md', '*.txt',
    '*.ts', '*.tsx', '*.js', '*.jsx', '*.vue', '*.scss', '*.css', '*.html', '*.ps1'
)

# Directories to skip
$skipDirectories = @(
    'node_modules', 'bin', 'obj', '.git', '.vs', 'packages',
    'TestResults', 'wwwroot\lib', 'dist', 'build'
)

$filesWithMarkerBytes = @()
$filesWithWrongLineEndings = @()
$filesProcessed = 0

Write-Host "Verifying line endings and checking for marker bytes..." -ForegroundColor Cyan
Write-Host "Repository root: $repoRoot" -ForegroundColor Gray
Write-Host ""

# Get all text files from repository root
$files = Get-ChildItem -Path $repoRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
    # Skip directories
    $skip = $false
    foreach ($dir in $skipDirectories) {
        if ($_.FullName -match [regex]::Escape($dir)) {
            $skip = $true
            break
        }
    }
    
    if ($skip) {
        return $false
    }
    
    # Check if extension is in text extensions list
    $isTextFile = $false
    foreach ($ext in $textExtensions) {
        if ($_.Name -like $ext) {
            $isTextFile = $true
            break
        }
    }
    
    return $isTextFile
}

foreach ($file in $files) {
    $filesProcessed++
    
    # Get relative path for display (use forward slashes for consistency)
    $relativePath = $file.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    
    try {
        # Determine expected line ending based on .gitattributes rules
        # The order matters - more specific rules should be checked first
        
        $isClientApp = $relativePath -match '^m4d/ClientApp/'
        $isYaml = $file.Extension -eq '.yml' -or $file.Extension -eq '.yaml'
        $isShellScript = $file.Extension -eq '.sh'
        
        # Check for files that should use LF (matching .gitattributes order)
        $shouldUseLF = $false
        
        if ($isClientApp) {
            # All ClientApp files use LF (most specific rule)
            $shouldUseLF = $true
        }
        elseif ($isYaml -or $isShellScript) {
            # YAML and shell scripts use LF
            $shouldUseLF = $true
        }
        # All other files use CRLF (default)
        
        # Read file as bytes to detect marker byte
        $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
        $hasMarkerByte = $bytes -contains 0x1A
        
        # Read file as text to check line endings
        $content = [System.IO.File]::ReadAllText($file.FullName)
        $hasCRLF = $content.Contains("`r`n")
        $hasLF = $content.Replace("`r`n", "").Contains("`n")
        $hasMixedLineEndings = $hasCRLF -and $hasLF
        
        if ($hasMarkerByte) {
            $filesWithMarkerBytes += $relativePath
            Write-Host "  ❌ Marker byte found: $relativePath" -ForegroundColor Red
        }
        
        $hasWrongLineEnding = $false
        if ($hasMixedLineEndings) {
            $hasWrongLineEnding = $true
            Write-Host "  ⚠️  Mixed line endings: $relativePath" -ForegroundColor Yellow
        } elseif ($shouldUseLF -and $hasCRLF) {
            $hasWrongLineEnding = $true
            Write-Host "  ⚠️  Should use LF but has CRLF: $relativePath" -ForegroundColor Yellow
        } elseif (-not $shouldUseLF -and $hasLF -and -not $hasCRLF) {
            $hasWrongLineEnding = $true
            Write-Host "  ⚠️  Should use CRLF but has LF: $relativePath" -ForegroundColor Yellow
        }
        
        if ($hasWrongLineEnding) {
            $filesWithWrongLineEndings += $relativePath
        }
    }
    catch {
        Write-Warning "  Failed to check $relativePath`: $_"
    }
}

Write-Host ""
Write-Host "Verification Summary:" -ForegroundColor Cyan
Write-Host "  Files checked: $filesProcessed" -ForegroundColor White
Write-Host "  Files with marker bytes (0x1A): $($filesWithMarkerBytes.Count)" -ForegroundColor $(if ($filesWithMarkerBytes.Count -gt 0) { 'Red' } else { 'Green' })
Write-Host "  Files with wrong line endings: $($filesWithWrongLineEndings.Count)" -ForegroundColor $(if ($filesWithWrongLineEndings.Count -gt 0) { 'Yellow' } else { 'Green' })

$totalIssues = $filesWithMarkerBytes.Count + $filesWithWrongLineEndings.Count

if ($totalIssues -eq 0) {
    Write-Host ""
    Write-Host "✅ All files have correct line endings and no marker bytes!" -ForegroundColor Green
    exit 0
} else {
    Write-Host ""
    Write-Host "❌ Found $totalIssues file(s) with issues." -ForegroundColor Red
    Write-Host "   Run 'scripts\normalize-line-endings.ps1 -Confirm:`$false' to fix these issues." -ForegroundColor Yellow
    
    if ($FailOnIssues) {
        exit 1
    }
    exit 0
}
