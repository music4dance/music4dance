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

# Enumerate the files git actually tracks, rather than walking the filesystem.
# A filesystem walk also picks up gitignored build output (m4d/wwwroot/vclient,
# e2e/test-results), restored vendor assets and scratch files under local/ - none of
# which git stores or normalizes. That made the check fail locally on files it has no
# business policing, while still passing in CI only because a fresh clone doesn't have
# them. Checking the tracked set makes local and CI runs agree.
Push-Location $repoRoot
try {
    $trackedPaths = @(& git ls-files --cached)
    if ($LASTEXITCODE -ne 0) {
        Write-Error "git ls-files failed - this script must be run inside a git working tree"
        exit 1
    }
}
finally {
    Pop-Location
}

$files = $trackedPaths | Where-Object { $_ } | ForEach-Object {
    # git ls-files always reports forward slashes, and Windows accepts them too, so
    # leave the separators alone. Rewriting them to backslashes builds paths that do
    # not exist on Linux, where CI runs - Test-Path then drops every nested file and
    # the check silently inspects almost nothing.
    Join-Path $repoRoot $_
} | Where-Object {
    # A tracked path can be absent from the working tree (e.g. a sparse checkout).
    Test-Path -LiteralPath $_ -PathType Leaf
} | ForEach-Object {
    # -Force so that dotfiles are not skipped as hidden: .editorconfig and
    # .gitattributes are tracked, and are hidden on Linux though not on Windows.
    Get-Item -LiteralPath $_ -Force
} | Where-Object {
    # Skip directories - matching whole path segments, not substrings. A substring
    # match against the full path quietly exempted every file whose *name* merely
    # contained a skip word: DanceObject.cs, ObjectHelpers.ts, DanceBuilder.cs and
    # distributed-attack-mitigation.md all contain "obj"/"build"/"dist", so they were
    # never checked at all.
    $relative = $_.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $directorySegments = '/' + [System.IO.Path]::GetDirectoryName($relative).Replace('\', '/') + '/'
    $skip = $false
    foreach ($dir in $skipDirectories) {
        if ($directorySegments -like ('*/' + $dir.Replace('\', '/') + '/*')) {
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
