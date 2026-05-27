<#
.SYNOPSIS
    Prepares the Line Dance Origins catalog for import into music4dance.

.DESCRIPTION
    Reads the customer-supplied "Line Dance Origins.tsv" and splits it into three
    upload-ready TSV files for the m4d admin UploadCatalog endpoint:

      Pass A  (line-dance-upload-a-songid.tsv)      — rows with known m4d SongIds
      Pass B  (line-dance-upload-b-spotify.tsv)     — rows with Spotify but no m4d link
      Pass C  (line-dance-upload-c-title-only.tsv)  — rows with neither ID

.PARAMETER SourceFile
    Path to the source TSV.  Defaults to <repo-root>/local/Line Dance Origins.tsv

.PARAMETER Username
    The m4d username to attribute the upload to.  Defaults to tzielund.

.PARAMETER OutputDir
    Directory where the three output TSV files are written.
    Defaults to <repo-root>/local

.EXAMPLE
    .\scripts\prepare-line-dance-upload.ps1

.EXAMPLE
    .\scripts\prepare-line-dance-upload.ps1 -Username dwgray

.NOTES
    Companion planning doc: local/line-dance-import-plan.md
    After running, upload files via Admin > Upload Catalog (no Dances or User form fields
    needed - both are embedded in the file as DANCE and USER columns).
    Recommended order: A first (highest confidence), then B, then C (review manually).

    Quoted cell values in the generated TSV are handled correctly by the server -
    CreatePropertiesFromRow strips surrounding quotes before processing.

    New Song.cs fields required before the upload will work:
      SPOTIFY     -> Purchase:00:SS  (track ID extracted here; already matched by MergeFromPurchaseInfo)
      SONGID      -> SongIdOverride  (direct SongId lookup, bypasses title matching)
      CHOREOGRAPHER -> Choreographer/PTN dance property (dance-level, like DanceComment)
      STEPSHEETURL  -> StepSheetUrl/PTN dance property (dance-level, like DanceComment)
      PATTERNNAME   -> PatternName/PTN dance property (the line/pattern dance name)

    DANCECOMMENT is formatted as "PatternName" by Choreographer so the comment text
    surfaces the dance identity inline wherever comments are displayed.

    DANCETAGS always includes Line Dance:Style (plus the difficulty tag when present).
#>

[CmdletBinding()]
param(
    [string]$SourceFile = (Join-Path $PSScriptRoot "..\local\Line Dance Origins.tsv"),
    [string]$Username   = 'tzielund',
    [string]$OutputDir  = (Join-Path $PSScriptRoot "..\local")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Helper functions
# ---------------------------------------------------------------------------

function Get-SongId {
    <# Extract the GUID from a music4dance song detail URL, or return '' #>
    param([string]$m4dLink)
    if ($m4dLink -match '(?i)/song/details/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})') {
        return $Matches[1]
    }
    return ''
}

function Get-SpotifyTrackId {
    <# Extract the track ID from a Spotify track URL, or return '' #>
    param([string]$spotifyUrl)
    if ($spotifyUrl -match 'open\.spotify\.com/track/([A-Za-z0-9]+)') {
        return $Matches[1]
    }
    return ''
}

function Split-ArtistAlbum {
    <#
    Splits cells like "Artist Name : (Album: Album Title)" into a hashtable
    with Artist and Album keys.  If the pattern is not found, Album is ''.
    #>
    param([string]$artistCell)
    if ($artistCell -match '^(.+?)\s*:\s*\(Album:\s*(.+?)\)\s*$') {
        return @{ Artist = $Matches[1].Trim(); Album = $Matches[2].Trim() }
    }
    return @{ Artist = $artistCell.Trim(); Album = '' }
}

function Get-CleanChoreographerName {
    <#
    Strips the country code and month/year suffix from choreographer strings.
    Input:  "Rachael McEnaney (USA) - April 2025"
    Output: "Rachael McEnaney"
    Handles multiple choreographers: "Alice (USA) & Bob (UK) - June 2025" -> "Alice & Bob"
    Passes through plain names like "Mary Zielund" unchanged.
    #>
    param([string]$raw)
    $clean = $raw.Trim()
    # Remove " - Month Year" suffix
    $clean = $clean -replace '\s*-\s+\w+ \d{4}\s*$', ''
    # Remove country codes in parens, e.g. "(USA)", "(UK)", "(CAN)"
    $clean = $clean -replace '\s*\([A-Z]{2,4}\)\s*', ' '
    # Normalize whitespace
    return ($clean -replace '\s+', ' ').Trim()
}

function Get-NormalizedDifficultyTag {
    <#
    Maps raw difficulty strings to canonical tag values used by m4d.
    Returns a DANCETAGS-style string like "Beginner:Other", or '' if unknown/blank.
    #>
    param([string]$raw)

    if ([string]::IsNullOrWhiteSpace($raw)) { return '' }

    # Strip everything after a slash (e.g. "Beginner/ Contra", "Beginner / 2 Wall")
    $clean = ($raw -replace '/.*$', '').Trim()

    # Strip trailing "Contra" qualifier (no slash) — Contra is a wall pattern, not a difficulty
    $clean = $clean -replace '\s+Contra\s*$', '' -replace '\s+contra\s*$', ''

    # Strip "Phrased" qualifier — it describes the dance form, not the difficulty level
    $clean = $clean -replace '\s+Phrased.*$', '' -replace '^Phrased\s+', ''
    $clean = $clean.Trim()

    $normalized = switch -Wildcard ($clean.ToLower()) {
        'ultra beginner'      { 'Absolute Beginner' }
        'absolute beginner'   { 'Absolute Beginner' }
        'hi beginner'         { 'High Beginner' }
        'high beginner'       { 'High Beginner' }
        'beginner warm up'    { 'Beginner' }
        'beginner'            { 'Beginner' }
        'high improver'       { 'High Improver' }
        'hi improver'         { 'High Improver' }
        'improver*'           { 'Improver' }   # covers "Improver AB!!" etc.
        'low intermediate'    { 'Low Intermediate' }
        'hi intermediate'     { 'High Intermediate' }
        'high intermediate'   { 'High Intermediate' }
        'intermediate'        { 'Intermediate' }
        'advanced'            { 'Advanced' }
        default               { '' }       # unrecognized values left blank
    }

    if ([string]::IsNullOrWhiteSpace($normalized)) { return '' }
    return "$normalized`:Other"
}

# ---------------------------------------------------------------------------
# Read source
# ---------------------------------------------------------------------------

if (-not (Test-Path $SourceFile)) {
    Write-Error "Source file not found: $SourceFile"
    exit 1
}

Write-Host "Reading: $SourceFile"
$rows = Import-Csv -Path $SourceFile -Delimiter "`t"
Write-Host "  $($rows.Count) data rows found."

if ($rows.Count -eq 0) {
    Write-Warning "Source file contains no data rows. Exiting."
    exit 1
}

# ---------------------------------------------------------------------------
# Column header discovery  — warn if expected columns are missing
# ---------------------------------------------------------------------------

$requiredColumns = @(
    'Dance Name', 'Choreographer', 'Difficulty', 'Song name', 'Artist',
    'Copperknob Link', 'Spotify Link', 'Music4Dance link'
)
$actualColumns = $rows[0].PSObject.Properties.Name
foreach ($col in $requiredColumns) {
    if ($col -notin $actualColumns) {
        Write-Warning "Expected column not found in source: '$col'"
    }
}

# ---------------------------------------------------------------------------
# Process rows and classify into passes A / B / C
# ---------------------------------------------------------------------------

$passA = [System.Collections.Generic.List[PSCustomObject]]::new()  # known SongId
$passB = [System.Collections.Generic.List[PSCustomObject]]::new()  # Spotify, no SongId
$passC = [System.Collections.Generic.List[PSCustomObject]]::new()  # no IDs

$unknownDifficulties = [System.Collections.Generic.SortedSet[string]]::new()
$artistsWithAlbum   = [System.Collections.Generic.List[string]]::new()

$rowNum = 0
foreach ($row in $rows) {
    $rowNum++

    $songId    = Get-SongId      ($row.'Music4Dance link')
    $spotifyId = Get-SpotifyTrackId ($row.'Spotify Link')
    $aa        = Split-ArtistAlbum  ($row.'Artist')
    $diffTag   = Get-NormalizedDifficultyTag ($row.'Difficulty')

    if ($aa.Album -ne '') {
        $artistsWithAlbum.Add("Row $rowNum`: $($row.'Artist')")
    }

    # Track raw difficulty values that could not be normalised (Get-NormalizedDifficultyTag returned '')
    $rawDiff = ($row.'Difficulty' -replace '/.*$', '').Trim()
    if ($rawDiff -ne '' -and $diffTag -eq '') {
        $unknownDifficulties.Add($rawDiff) | Out-Null
    }

    $choreographer = Get-CleanChoreographerName ($row.'Choreographer')
    $patternName   = $row.'Dance Name'.Trim()

    # Format DANCECOMMENT as "PatternName" by Choreographer.
    # The TSV parser in Song.cs uses RFC 4180 unquoting, so plain ASCII double quotes
    # survive Export-Csv round-tripping correctly.
    $danceComment = if ($choreographer -ne '') {
        "`"$patternName`" by $choreographer"
    } else {
        "`"$patternName`""
    }

    # DANCETAGS: always include Line Dance:Style; append difficulty when known
    $danceTags = if ($diffTag -ne '') { "Line Dance:Style|$diffTag" } else { 'Line Dance:Style' }

    $uploadRow = [PSCustomObject]@{
        USER           = $Username
        DANCE          = 'PTN'
        SONGID         = $songId
        TITLE          = $row.'Song name'.Trim()
        ARTIST         = $aa.Artist
        ALBUM          = $aa.Album
        SPOTIFY        = $spotifyId
        DANCECOMMENT   = $danceComment
        PATTERNNAME    = $patternName
        CHOREOGRAPHER  = $choreographer
        STEPSHEETURL   = $row.'Copperknob Link'.Trim()
        DANCETAGS      = $danceTags
    }

    if ($songId -ne '') {
        $passA.Add($uploadRow)
    } elseif ($spotifyId -ne '') {
        $passB.Add($uploadRow)
    } else {
        $passC.Add($uploadRow)
    }
}

# ---------------------------------------------------------------------------
# Write output files
# ---------------------------------------------------------------------------

$null = New-Item -ItemType Directory -Force -Path $OutputDir

$fileA = Join-Path $OutputDir 'line-dance-upload-a-songid.tsv'
$fileB = Join-Path $OutputDir 'line-dance-upload-b-spotify.tsv'
$fileC = Join-Path $OutputDir 'line-dance-upload-c-title-only.tsv'

# Export-Csv always uses the property order from the first object, which is what we want.
$passA | Export-Csv -Path $fileA -Delimiter "`t" -NoTypeInformation -Encoding UTF8
$passB | Export-Csv -Path $fileB -Delimiter "`t" -NoTypeInformation -Encoding UTF8
$passC | Export-Csv -Path $fileC -Delimiter "`t" -NoTypeInformation -Encoding UTF8

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "=== Output Summary ==="
Write-Host "  Pass A (known SongId) : $($passA.Count) rows -> $fileA"
Write-Host "  Pass B (Spotify only) : $($passB.Count) rows -> $fileB"
Write-Host "  Pass C (no IDs)       : $($passC.Count) rows -> $fileC"
Write-Host "  Total                 : $($passA.Count + $passB.Count + $passC.Count) rows"
Write-Host ""

if ($artistsWithAlbum.Count -gt 0) {
    Write-Host "=== Artist cells with embedded album info (split into ARTIST + ALBUM) ==="
    foreach ($entry in $artistsWithAlbum) {
        Write-Host "  $entry"
    }
    Write-Host ""
}

if ($unknownDifficulties.Count -gt 0) {
    Write-Host "=== Unknown difficulty values (passed through as-is - review these) ==="
    foreach ($d in $unknownDifficulties) {
        Write-Host "  '$d'"
    }
    Write-Host ""
}

if ($passC.Count -gt 0) {
    Write-Host "=== Pass C rows (no IDs - review before uploading) ==="
    Write-Host "  Choreography Name                Title                     Artist"
    Write-Host "  --------------------------------  ------------------------  --------"
    foreach ($r in $passC) {
        $chName  = $r.DANCECOMMENT.PadRight(32).Substring(0, 32)
        $title   = $r.TITLE.PadRight(24).Substring(0, 24)
        $artist  = $r.ARTIST
        Write-Host "  $chName  $title  $artist"
    }
    Write-Host ""
}

Write-Host 'Done. Upload order: A (highest confidence) -> B -> C (review first).'
