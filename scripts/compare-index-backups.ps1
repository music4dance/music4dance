<#
.SYNOPSIS
    Compares two /Admin/IndexBackup output files line-by-line and classifies any differences.

.DESCRIPTION
    Each line is "SongId={guid}<TAB><properties log>" (see Song.Serialize). Assumes both files
    list songs in the same order (verify this holds -- e.g. same line count, matching first/last
    lines -- before trusting the line-by-line comparison; this script does not realign on SongId).

    A difference is classified as "expected" when the OLD line's properties are exactly the NEW
    line's properties with a stray "SongId=<guid><TAB>" glued onto the front -- the legacy
    AdminEdit(string, ...) bug that SongPropertyCompression.Decompress strips on read (see
    architecture/song-internal-format.md S11.1). Anything else is reported as unexplained.

.PARAMETER OldPath
    Path to the earlier backup file.

.PARAMETER NewPath
    Path to the later backup file.

.PARAMETER OutFile
    Optional path to write the report to. If omitted, prints to the console.

.PARAMETER MaxSamples
    Maximum number of unexplained-diff samples to include in the report. Defaults to 25.

.EXAMPLE
    ./scripts/compare-index-backups.ps1 -OldPath local/index-2026-07-04.txt -NewPath local/index-2026-07-08.txt -OutFile local/index-diff-report.txt
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$OldPath,

    [Parameter(Mandatory = $true)]
    [string]$NewPath,

    [string]$OutFile,

    [int]$MaxSamples = 25
)

if (-not (Test-Path $OldPath)) {
    throw "File not found: $OldPath"
}
if (-not (Test-Path $NewPath)) {
    throw "File not found: $NewPath"
}

function Split-SongLine([string]$line) {
    $tab = $line.IndexOf("`t")
    if ($tab -eq -1) {
        return @{ Id = $line; Properties = "" }
    }
    return @{ Id = $line.Substring(0, $tab); Properties = $line.Substring($tab + 1) }
}

$oldReader = [System.IO.File]::OpenText((Resolve-Path $OldPath))
$newReader = [System.IO.File]::OpenText((Resolve-Path $NewPath))

$lineNo = 0
$diffCount = 0
$expectedCount = 0
$unexpectedCount = 0
$idMismatchCount = 0
$lengthMismatch = $false
$samples = [System.Collections.Generic.List[string]]::new()

try {
    while ($true) {
        $oldLine = $oldReader.ReadLine()
        $newLine = $newReader.ReadLine()
        $lineNo++

        if ($null -eq $oldLine -and $null -eq $newLine) {
            $lineNo--
            break
        }
        if ($null -eq $oldLine -or $null -eq $newLine) {
            $lengthMismatch = $true
            break
        }

        if ($oldLine -eq $newLine) {
            continue
        }

        $diffCount++

        $old = Split-SongLine $oldLine
        $new = Split-SongLine $newLine

        if ($old.Id -ne $new.Id) {
            $idMismatchCount++
            if ($samples.Count -lt $MaxSamples) {
                $samples.Add("Line $lineNo`: SongId mismatch (files may not be in the same order) -- old=$($old.Id) new=$($new.Id)")
            }
            continue
        }

        $strayPrefix = "SongId="
        $isStray = $false
        if ($old.Properties.StartsWith($strayPrefix)) {
            $innerTab = $old.Properties.IndexOf("`t")
            if ($innerTab -ge 0) {
                $stripped = $old.Properties.Substring($innerTab + 1)
                if ($stripped -eq $new.Properties) {
                    $isStray = $true
                }
            }
        }

        if ($isStray) {
            $expectedCount++
        }
        else {
            $unexpectedCount++
            if ($samples.Count -lt $MaxSamples) {
                $oldPreview = if ($old.Properties.Length -gt 300) { $old.Properties.Substring(0, 300) + "..." } else { $old.Properties }
                $newPreview = if ($new.Properties.Length -gt 300) { $new.Properties.Substring(0, 300) + "..." } else { $new.Properties }
                $samples.Add("Line $lineNo ($($old.Id)):`n  OLD: $oldPreview`n  NEW: $newPreview")
            }
        }
    }
}
finally {
    $oldReader.Close()
    $newReader.Close()
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("Compared $lineNo lines from:")
$lines.Add("  OLD: $OldPath")
$lines.Add("  NEW: $NewPath")
if ($lengthMismatch) {
    $lines.Add("")
    $lines.Add("WARNING: files have different line counts -- comparison stopped at line $lineNo.")
}
$lines.Add("")
$lines.Add("Differing lines:       $diffCount")
$lines.Add("  Stray-SongId cleanup (expected): $expectedCount")
$lines.Add("  SongId mismatch (likely reordered/misaligned): $idMismatchCount")
$lines.Add("  Unexplained:                     $unexpectedCount")

if ($samples.Count -gt 0) {
    $lines.Add("")
    $lines.Add("Samples (up to $MaxSamples):")
    $lines.AddRange($samples)
}

if ($OutFile) {
    $lines | Set-Content -Path $OutFile -Encoding utf8
    Write-Host "Wrote report to $OutFile"
}
else {
    $lines | ForEach-Object { Write-Host $_ }
}
