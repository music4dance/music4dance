<#
.SYNOPSIS
    Computes line-length statistics for a text file, bucketed by a configurable char width.

.PARAMETER Path
    Path to the file to analyze.

.PARAMETER BucketSize
    Bucket width in characters. Defaults to 1000.

.PARAMETER OutFile
    Optional path to write the detailed report to. If omitted, prints to the console.

.EXAMPLE
    ./scripts/line-length-stats.ps1 -Path architecture/local/build-out/index-2026-07-02.txt -OutFile local/line-length-stats.txt
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [int]$BucketSize = 1000,

    [string]$OutFile
)

if (-not (Test-Path $Path)) {
    throw "File not found: $Path"
}

$buckets = @{}
$totalLines = 0
$sumLen = 0
$maxLen = 0
$maxLineNo = 0

$reader = [System.IO.File]::OpenText((Resolve-Path $Path))
try {
    $lineNo = 0
    while ($null -ne ($line = $reader.ReadLine())) {
        $lineNo++
        $len = $line.Length
        $totalLines++
        $sumLen += $len
        if ($len -gt $maxLen) {
            $maxLen = $len
            $maxLineNo = $lineNo
        }
        $bucket = [int](($len - ($len % $BucketSize)) / $BucketSize)
        if ($buckets.ContainsKey($bucket)) {
            $buckets[$bucket]++
        } else {
            $buckets[$bucket] = 1
        }
    }
} finally {
    $reader.Close()
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("Total lines: $totalLines")
$lines.Add("Max line length: $maxLen (line $maxLineNo)")
if ($totalLines -gt 0) {
    $avg = [math]::Round($sumLen / $totalLines, 1)
    $lines.Add("Average line length: $avg")
}
$lines.Add("")
$lines.Add(("{0,-20}{1,10}{2,10}" -f "Bucket (chars)", "Count", "Pct"))

foreach ($bucket in ($buckets.Keys | Sort-Object)) {
    $lo = $bucket * $BucketSize
    $hi = $lo + $BucketSize
    $count = $buckets[$bucket]
    $pct = [math]::Round((100 * $count / $totalLines), 2)
    $label = "{0}k-{1}k" -f [int]($lo / 1000), [int]($hi / 1000)
    $lines.Add(("{0,-20}{1,10}{2,9}%" -f $label, $count, $pct))
}

if ($OutFile) {
    $lines | Set-Content -Path $OutFile -Encoding utf8
    Write-Host "Wrote report to $OutFile"
} else {
    $lines | ForEach-Object { Write-Host $_ }
}
