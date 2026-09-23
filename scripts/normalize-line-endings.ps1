<#
.SYNOPSIS
    Normalizes line endings and removes corrupt marker bytes from all text files.
.DESCRIPTION
    This script fixes line ending inconsistencies and removes the corrupt 0x1A marker byte
    that was used in the old SongFilter implementation. It processes all text files in the
    repository while preserving binary files.
    
    Files are normalized according to .gitattributes:
    - .yml, .yaml files: LF (Linux/CI environments)
    - ClientApp files: LF (Vue.js convention)
    - Other files: CRLF (Windows convention)
.PARAMETER WhatIf
    Shows what would be changed without actually making changes.
.PARAMETER Verbose
    Shows detailed output for each file processed.
#>
[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = "Stop"

Write-Host "Starting line ending normalization..." -ForegroundColor Cyan
Write-Host "Script directory: $PSScriptRoot" -ForegroundColor Gray

# Find repository root (look for .git directory)
$scriptDir = $PSScriptRoot
$repoRoot = $scriptDir

Write-Host "Searching for repository root..." -ForegroundColor Gray
while ($repoRoot -and -not (Test-Path (Join-Path $repoRoot ".git"))) {
    Write-Verbose "Checking: $repoRoot"
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

Write-Host "Repository root found: $repoRoot" -ForegroundColor Green
Write-Host ""

# File extensions to process (ClientApp files and .yml will use LF, others CRLF)
$textExtensions = @(
    '*.cs', '*.cshtml', '*.csproj', '*.sln', '*.config',
    '*.json', '*.xml', '*.yml', '*.yaml', '*.md', '*.txt',
    '*.ts', '*.tsx', '*.js', '*.jsx', '*.vue', '*.scss', '*.css', '*.html', '*.ps1'
)

# Directories to skip (removed 'scripts' from the list)
$skipDirectories = @(
    'node_modules', 'bin', 'obj', '.git', '.vs', 'packages',
    'TestResults', 'wwwroot\lib', 'dist', 'build'
)

$filesModified = 0
$filesSkipped = 0
$filesProcessed = 0
$markerBytesFound = 0
$mixedLineEndingsFound = 0

Write-Host "Scanning for files..." -ForegroundColor Cyan

# Enumerate the files git actually tracks. A filesystem walk would also rewrite
# gitignored build output, restored vendor assets and scratch files under local/,
# churning files git does not store. This must stay in step with
# verify-line-endings.ps1, which checks the same set.
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

$allFiles = @($trackedPaths | Where-Object { $_ } | ForEach-Object {
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
})
Write-Host "Found $($allFiles.Count) tracked files" -ForegroundColor Gray

$files = $allFiles | Where-Object {
    # Skip directories - matching whole path segments, not substrings, so that files
    # merely *named* like a skip word (DanceObject.cs, ObjectHelpers.ts,
    # DanceBuilder.cs) are not silently exempted. Must match verify-line-endings.ps1.
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

Write-Host "Found $($files.Count) text files to process" -ForegroundColor Gray
Write-Host ""

if ($files.Count -eq 0) {
    Write-Warning "No files found to process!"
    exit 0
}

foreach ($file in $files) {
    $filesProcessed++
    
    # Get relative path for display
    $relativePath = $file.FullName.Substring($repoRoot.Length + 1)
    
    if ($filesProcessed % 100 -eq 0) {
        Write-Host "  Processed $filesProcessed files..." -ForegroundColor Gray
    }
    
    Write-Verbose "Processing: $relativePath"
    
    try {
        # Determine target line ending based on file type and location
        # Match on a separator-normalized relative path, anchored, exactly as
        # verify-line-endings.ps1 does. Matching 'm4d\ClientApp' against the full path
        # never matched on Linux or macOS, where the separator is '/', so every ClientApp
        # file was treated as CRLF and rewritten - the opposite of what .gitattributes
        # requires, across 430 files.
        $relativeForMatch = $relativePath.Replace('\', '/')
        $isClientApp = $relativeForMatch -match '^m4d/ClientApp/'
        $isYaml = $file.Extension -eq '.yml' -or $file.Extension -eq '.yaml'
        $isShellScript = $file.Extension -eq '.sh'
        
        # ClientApp, YAML and shell files use LF, everything else uses CRLF
        $useLF = $isClientApp -or $isYaml -or $isShellScript
        $targetLineEnding = if ($useLF) { "`n" } else { "`r`n" }
        $targetName = if ($useLF) { "LF" } else { "CRLF" }
        
        # Read file as bytes to detect marker byte
        $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
        $hasMarkerByte = $bytes -contains 0x1A
        
        # Read file as text to check line endings
        $content = [System.IO.File]::ReadAllText($file.FullName)
        $hasCRLF = $content.Contains("`r`n")
        $hasLF = $content.Replace("`r`n", "").Contains("`n")
        $hasMixedLineEndings = $hasCRLF -and $hasLF
        
        $needsChange = $false
        $changes = @()
        
        if ($hasMarkerByte) {
            $markerBytesFound++
            $needsChange = $true
            $changes += "marker byte (0x1A)"
        }
        
        if ($hasMixedLineEndings) {
            $mixedLineEndingsFound++
            $needsChange = $true
            $changes += "mixed line endings"
        } elseif ($useLF -and $hasCRLF) {
            $needsChange = $true
            $changes += "CRLF → LF"
        } elseif (-not $useLF -and -not $hasCRLF -and $hasLF) {
            $needsChange = $true
            $changes += "LF → CRLF"
        }
        
        if ($needsChange) {
            $filesModified++
            $changeDesc = $changes -join ", "
            Write-Host "  Fixing $relativePath`: $changeDesc" -ForegroundColor Yellow
            
            # Use -Confirm:$false to avoid prompting
            if ($PSCmdlet.ShouldProcess($relativePath, "Normalize line endings to $targetName")) {
                # Remove marker byte
                if ($hasMarkerByte) {
                    $content = [System.Text.Encoding]::UTF8.GetString($bytes).Replace([char]0x1A, '')
                }
                
                # Normalize line endings
                $content = $content.Replace("`r`n", "`n")
                if ($targetLineEnding -eq "`r`n") {
                    $content = $content.Replace("`n", "`r`n")
                }
                
                # Write back with UTF-8 encoding (no BOM for most files)
                $utf8NoBom = New-Object System.Text.UTF8Encoding $false
                [System.IO.File]::WriteAllText($file.FullName, $content, $utf8NoBom)
            }
        } else {
            Write-Verbose "  No changes needed: $relativePath"
        }
    }
    catch {
        Write-Warning "  Failed to process $relativePath`: $_"
        $filesSkipped++
    }
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Files processed: $filesProcessed" -ForegroundColor White
Write-Host "  Files modified: $filesModified" -ForegroundColor $(if ($filesModified -gt 0) { 'Yellow' } else { 'Green' })
Write-Host "  Files skipped: $filesSkipped" -ForegroundColor Gray
Write-Host "  Files with marker bytes: $markerBytesFound" -ForegroundColor $(if ($markerBytesFound -gt 0) { 'Red' } else { 'Green' })
Write-Host "  Files with mixed line endings: $mixedLineEndingsFound" -ForegroundColor $(if ($mixedLineEndingsFound -gt 0) { 'Yellow' } else { 'Green' })

if ($WhatIfPreference) {
    Write-Host ""
    Write-Host "This was a dry run. Run without -WhatIf to apply changes." -ForegroundColor Cyan
}
