# MusicService Class Model and Data Encoding

> For the higher-level integration overview (registered services, HTTP infrastructure, purchase filtering) see [music-service-integration.md](music-service-integration.md).

## Class Hierarchy

```
MusicService (abstract base, m4dModels/MusicService.cs)
├── ITunesService      (m4dModels/ITunesService.cs)    CID='I'  IsIndexed  CanSearchExternally
├── SpotifyService     (m4dModels/SpotifyService.cs)   CID='S'  IsIndexed  CanSearchExternally
├── ISRCService        (m4dModels/ISRCService.cs)      CID='R'  IsIndexed  (no external search)
├── AmazonService      (m4dModels/AmazonService.cs)    CID='A'  (not indexed, no external search)
└── MusicServiceStub                                    CID=E/P/M  (not indexed, no external search)
    (EMusic, Pandora, AMG — historical stubs)
```

All instances are registered once at static-class init time and looked up by `ServiceType` enum or `CID` character:

```csharp
MusicService.GetService(ServiceType.Spotify)   // by enum
MusicService.GetService('S')                   // by CID char
MusicService.GetIndexedServices()              // iTunes + Spotify + ISRC (Azure Search ServiceIds)
MusicService.GetSearchableServices()           // iTunes + Spotify only (external enrichment loop)
```

### IsIndexed vs CanSearchExternally

`MusicService` has two boolean flags that independently control how a service participates in the pipeline:

| Property | Default | Controls |
| --- | --- | --- |
| `IsIndexed` | `true` | Whether `GetExtendedPurchaseIds()` includes this service's IDs in the Azure Search `ServiceIds` field |
| `CanSearchExternally` | `IsIndexed` | Whether the enrichment loop (`UpdateSongAndServices` / `ConditionalUpdateSongAndServices`) calls this service's external API |

`ISRCService` sets `CanSearchExternally = false` because there is no ISRC search API — ISRCs are read from Spotify track metadata by `GetISRCData` and never looked up independently. This prevents the enrichment loop from calling `ParseSearchResults` on ISRC (which would always return empty) and then persisting a spurious `RecordFail('R')` entry on every song.

## Base Class Responsibility

`MusicService` provides two things:

1. **URL templates** — `SearchRequest`, `TrackRequest`, and `AssociateLink` are format strings where `{0}` is the URL-encoded search term or ID. `BuildSearchRequest`, `BuildTrackRequest`, and `BuildPurchaseLink` apply them.

2. **Parse hooks** — `ParseSearchResults` and `ParseTrackResults` are abstract; each concrete service implements them to translate its own JSON shape into `ServiceTrack` objects.

Additional overridable hooks: `BuildLookupRequest` (album/playlist URLs), `GetNextRequest` (pagination cursor), `NormalizeId` (ID canonicalization), `PreprocessResponse` (JSON fixup before parsing).

## ServiceTrack — The Intermediate DTO

`ServiceTrack` (m4dModels/ServiceTrack.cs) is what every parse method produces. It carries one recording as seen by the external service:

| Field          | Source (iTunes)        | Source (Spotify)               |
| -------------- | ---------------------- | ------------------------------ |
| `Service`      | `ServiceType.ITunes`   | `ServiceType.Spotify`          |
| `TrackId`      | `trackId`              | `id`                           |
| `CollectionId` | `collectionId`         | `album.id`                     |
| `Name`         | `trackName`            | `name`                         |
| `Artist`       | `artistName`           | `artists[0].name`              |
| `Album`        | `collectionName`       | `album.name`                   |
| `Duration`     | `trackTimeMillis÷1000` | `duration_ms÷1000`             |
| `TrackNumber`  | `trackNumber`          | `track_number` (+ disc offset) |
| `Genres`       | `[primaryGenreName]`   | fetched from album/artist hrefs |
| `SampleUrl`    | `PreviewUrl`           | `preview_url`                  |
| `ImageUrl`     | `artworkUrl30`         | smallest image in `album.images` |
| `IsPlayable`   | (not present)          | `is_playable`                  |
| `AudioData`    | (filled later)         | `EchoTrack` (filled separately)|

`AltId` is reserved for AMG IDs (no longer populated). `AlbumLink`, `SongLink`, and `PurchaseInfo` are computed by `MusicServiceManager.ComputeTrackPurchaseInfo` after search results are returned.

## EchoTrack — Spotify Audio Features

`EchoTrack` (m4dModels/EchoTrack.cs) holds data from `GET /v1/audio-features/{id}`:

| Field            | Spotify JSON field  | Notes                                          |
| ---------------- | ------------------- | ---------------------------------------------- |
| `BeatsPerMeasure`| `time_signature`    | Numerator only (4 for 4/4, 3 for 3/4, etc.)   |
| `BeatsPerMinute` | `tempo`             | Stored on `Song.Tempo` (as BPM, not MPM)       |
| `Danceability`   | `danceability`      | 0.0–1.0 float                                  |
| `Energy`         | `energy`            | 0.0–1.0 float                                  |
| `Valence`        | `valence`           | 0.0–1.0 float                                  |

The computed `Meter` property converts `BeatsPerMeasure` to a time-signature string: 2/3/4 → `"N/4"`, 6/9/12 → `"N/8"`. This string is stored as a tag (`"4/4:Tempo"`) via `GetEchoData`.

## AlbumDetails — Per-Album Persistence Model

`AlbumDetails` (m4dModels/AlbumDetails.cs) represents one album occurrence on a song:

```
AlbumDetails
├── Name        string    — album title
├── Publisher   string    — label / publisher
├── Track       int?      — track number (may encode disc via TrackNumber struct)
├── Index       int       — 0-based slot index for serialization ordering
└── Purchase    Dictionary<string, string>   — purchase IDs keyed by 2-char code
```

### Purchase Dictionary Key Format

The key is two characters: `CID + PurchaseTypeChar`:

| Key  | Meaning                        |
| ---- | ------------------------------ |
| `IS` | iTunes Song ID (trackId)       |
| `IA` | iTunes Album ID (collectionId) |
| `SS` | Spotify Track ID               |
| `SA` | Spotify Album ID               |
| `AS` | Amazon Song ID (ASIN)          |
| `AA` | Amazon Album ID (ASIN)         |
| `MS` | AMG Song ID (legacy)           |

`MusicService.BuildPurchaseKey(PurchaseType)` builds the key; `TryParsePurchaseType` reverses it.

### Multi-ID Accumulation

Services (Spotify in particular) periodically reissue a new ID for the same recording. `AddPurchaseId` appends to the slot rather than overwriting — IDs are comma-separated in the dictionary value:

```
Purchase["SS"] = "3X2p7fCVH4g5ITBGH8pEtZ,4CKG8aT2vJKB0vhNUUb2aQ"
```

`GetPurchaseIdentifiers(service, type)` returns all IDs. `GetPurchaseIdentifier(service, type)` returns only the first (primary) one, which is used for link generation.

## SongProperty Encoding

`Song` stores its data as a flat list of `SongProperty` records — the append-only event log that drives all persistence. Each property has:

```
Name  = BaseName[:idx[:qualifier]]
Value = string
```

The `SongProperty.FormatName` helper constructs names:

```csharp
SongProperty.FormatName("Purchase", index: 0, qualifier: "SS")
// → "Purchase:00:SS"
```

### Album and Purchase Properties

When `AlbumDetails.CreateProperties(song)` serializes an album, it emits:

| SongProperty Name          | Value          | Meaning                             |
| -------------------------- | -------------- | ----------------------------------- |
| `Album:00`                 | "Now Dancing"  | Album name, slot 0                  |
| `Track:00`                 | "5"            | Track number, slot 0                |
| `Publisher:00`             | "Sony"         | Publisher, slot 0                   |
| `Purchase:00:IS`           | "1234567890"   | iTunes Song ID for album slot 0     |
| `Purchase:00:IA`           | "9876543210"   | iTunes Collection ID for album slot 0|
| `Purchase:00:SS`           | "3X2p7fCVH4g5…"| Spotify Track ID for album slot 0   |

When a purchase ID is removed, a mirroring `Purchase-` property is emitted:

```
Purchase-:00:SS = 3X2p7fCVH4g5…
```

`BuildAlbumInfo` reconstructs `AlbumDetails` objects from these properties when a song is loaded.

### Field Name Constants on Song

```csharp
Song.AlbumField     = "Album"
Song.TrackField     = "Track"
Song.PublisherField = "Publisher"
Song.PurchaseField  = "Purchase"
Song.RemovedPurchaseField = "Purchase-"
```

`Song.IsAlbumField(fieldName)` returns true for all of the above plus `AlbumList`, `PromoteAlbum`, `AlbumOrder`.

## Data Flow: API → Song

```
MusicServiceManager.FindMusicServiceSong
    └─ service.ParseSearchResults / ParseTrackResults
           └─ List<ServiceTrack>

MusicServiceManager.UpdateMusicService(song, service, ..., trackId, collectionId)
    └─ AlbumDetails.AddPurchaseId(PurchaseType.Song,  service.Id, trackId)
    └─ AlbumDetails.AddPurchaseId(PurchaseType.Album, service.Id, collectionId)

dms.SongIndex.EditSong(user, old, edit, tags)
    └─ AlbumDetails.PurchaseDiff / CreateProperties
           └─ song.CreateProperty("Purchase:00:SS", trackId)
                  └─ stored in Song.SongProperties
```

After `EditSong`, `UpdateAudioData` fills `Tempo`, `Danceability`, `Energy`, `Valence`, and the meter tag for Spotify tracks, and `Sample` for the preview URL.
