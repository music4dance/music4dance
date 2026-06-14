<#
.SYNOPSIS
    Builds an UploadCatalog TSV from the Nine Sinatra Songs playlist page.

.DESCRIPTION
    Reads the music4dance playlist page, extracts song rows from the embedded
    model_ JSON payload, and writes a TSV suitable for UploadCatalog.

    Output fields:
      SONGID, TITLE, ARTIST, DANCE, DANCECOMMENT, CHOREOGRAPHER, PATTERNNAME, DANCETAGS

    Dance-scoped values are emitted using UploadCatalog headers so they map to:
      Comment+:BLT, Choreographer+:BLT, PatternName+:BLT

.PARAMETER PlaylistUrl
    Playlist page URL. Defaults to Nine Sinatra Songs playlist.

.PARAMETER OutputFile
    Output TSV path. Defaults to <repo>/local/nine-sinatra-songs-upload.tsv

.EXAMPLE
    .\scripts\prepare-nine-sinatra-upload.ps1

.EXAMPLE
    .\scripts\prepare-nine-sinatra-upload.ps1 -OutputFile .\local\nine-sinatra.tsv
#>

[CmdletBinding()]
param(
    [string]$PlaylistUrl = 'https://www.music4dance.net/song/playlist?id=4PGMLc2lfy6hsdmyj2rm2x',
    [string]$OutputFile = (Join-Path $PSScriptRoot '..\local\nine-sinatra-songs-upload.tsv')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-PropertyValue {
    param(
        [Parameter(Mandatory)] $Properties,
        [Parameter(Mandatory)] [string]$Name
    )

    $match = $Properties | Where-Object { $_.name -eq $Name } | Select-Object -First 1
    if ($null -eq $match) {
        return ''
    }

    return [string]$match.value
}

Write-Host "Reading playlist page: $PlaylistUrl"
$response = Invoke-WebRequest -Uri $PlaylistUrl -TimeoutSec 60
$content = $response.Content

# The page embeds song data in: <script>var model_ = { ... };</script>
$modelMatch = [regex]::Match(
    $content,
    '(?s)<script>\s*var\s+model_\s*=\s*(\{.*?\})\s*;\s*</script>'
)

if (-not $modelMatch.Success) {
    throw 'Could not find embedded model_ JSON in the playlist page.'
}

$modelJson = $modelMatch.Groups[1].Value
$model = $modelJson | ConvertFrom-Json

if ($null -eq $model.histories -or $model.histories.Count -eq 0) {
    throw 'No song histories found in embedded model_ JSON.'
}

$commentValue = '"Nine Sinatra Songs" by Twyla Tharp'
$choreographerValue = 'Twyla Tharp'
$patternNameValue = 'Nine Sinatra Songs'
$danceId = 'BLT'
$danceTags = 'PNB:Other'

$rows = [System.Collections.Generic.List[PSCustomObject]]::new()
$seenIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($history in $model.histories) {
    $songId = [string]$history.id
    if ([string]::IsNullOrWhiteSpace($songId)) {
        continue
    }

    if (-not $seenIds.Add($songId)) {
        continue
    }

    $title = Get-PropertyValue -Properties $history.properties -Name 'Title'
    if ([string]::IsNullOrWhiteSpace($title)) {
        continue
    }

    $artist = Get-PropertyValue -Properties $history.properties -Name 'Artist'

    $rows.Add([PSCustomObject]@{
        SONGID       = $songId
        TITLE        = $title
        ARTIST       = $artist
        DANCE        = $danceId
        DANCECOMMENT = $commentValue
        CHOREOGRAPHER = $choreographerValue
        PATTERNNAME  = $patternNameValue
        DANCETAGS    = $danceTags
    })
}

if ($rows.Count -eq 0) {
    throw 'No songs with Title were extracted from the playlist model.'
}

$null = New-Item -Path (Split-Path -Parent $OutputFile) -ItemType Directory -Force
$rows | Export-Csv -Path $OutputFile -Delimiter "`t" -NoTypeInformation -Encoding UTF8

Write-Host "Wrote $($rows.Count) rows to: $OutputFile"
