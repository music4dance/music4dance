# Index Backup Streaming

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01
**Code:** `m4dModels/SongIndex.cs` (`BackupIndexStreamingAsync`, `StreamAllSongsAsync`, `UploadIndex`),
`m4dModels/DanceMusicCoreService.cs` (`CloneIndex`, `UpdateIndex`), `m4d/Controllers/AdminController.cs` (`IndexBackup`)

How music4dance reads *every* song out of an Azure AI Search index, with no size limit: for
backups, index-to-index copies, schema migrations, and full reload passes.

## The problem it solves

Azure AI Search rejects `$skip` above 100,000 (`InvalidRequestParameter: Value must be between 0
and 100000 for $skip`), and production has more songs than that. The old `BackupIndex` loaded
every result into memory with skip-based paging, so it failed past 100K and exhausted memory.

## Composite key-set pagination

`SongIndex.BackupIndexStreamingAsync(filter = null, cancellationToken)` returns an
`IAsyncEnumerable<string>` of serialized songs, most recently modified first:

1. It requests pages of 1,000 with `OrderBy = Modified desc, SongId desc` and no `Skip`. It
   selects only `SongId`, `Modified` and `Properties`.
2. After each page, it remembers the last row's `Modified` and `SongId`. The next page's filter
   becomes `(Modified lt last) or (Modified eq last and SongId lt 'lastId')`, ANDed with any
   caller-supplied `SongFilter` OData filter.
3. Each row is yielded as `Song.Serialize(id, SongPropertyCompression.Decompress(properties))`,
   which is the same line format as a backup file (see
   [song-internal-format](../songs/song-internal-format.md)).
4. It stops when a page returns fewer than 1,000 rows.

Every page goes through `DoSearch`, so a full pass gets the same retry and service-health
handling as live search (see [service-resilience](../infrastructure/service-resilience.md)).
Memory stays at one page, whatever the index size.

`StreamAllSongsAsync` uses the same pagination but yields full `Song` objects. It's used by
full-index reload passes (`AdminController.ReloadAllSongs`) that re-read and re-save every row,
for example to pick up a new `Properties` compression format.

## Callers

| Caller | Purpose |
| --- | --- |
| `GET /Admin/IndexBackup?name=&filter=&writeBufferSize=` (`showDiagnostics`) | Writes a backup file, optionally filtered, in buffered chunks (`writeBufferSize` clamped to 1–1000), with progress through `AdminMonitor` and cancellation support |
| `DanceMusicCoreService.CloneIndex(to)` | Copies the current index into another, piping the stream straight into `UploadIndex` |
| `DanceMusicCoreService.UpdateIndex` | The schema-migration copy into the next-version index (see [runbooks/search-index-breaking-migration](../runbooks/search-index-breaking-migration.md)) |
| `DanceMusicService.SerializeSongs` | Song sections of admin database backups and exports (`AdminController`) |

`SongIndex.UploadIndex(IAsyncEnumerable<string> lines, trackDeleted)` is the matching writer.
It's also used by the admin upload of a backup file.

## Design notes

- **Why not continuation tokens:** the SDK's `AsPages()` only ever returned the first page, with
  a null continuation token, on this service tier.
- **Why a composite key:** paging on `SongId gt last` alone works but loses date ordering.
  Adding `Modified` keeps backups newest-first and deterministic, and `SongId` breaks ties.
- Dates in filters are ISO 8601 (`ToString("o")`).

## Future improvements

- **Resume:** checkpoint the last `(Modified, SongId)` and accept a `startAfter`, to restart a
  partial backup.
- **Run very large backups as a background task**, with status UI and a completion notice.

## History

- 2025-02-26: Streaming backup implemented and verified on production (102,605+ songs). Three
  approaches were tried: continuation tokens (failed), `SongId`-only key-set (lost ordering), and
  the composite key (shipped). All three callers moved over, and `BackupIndex` was removed.
- Later: Paging routed through `DoSearch` for retry and health handling. `StreamAllSongsAsync`
  added for reload passes.
- 2026-10-01: Rewritten from the original plan as a current-state reference.

## Related

- [search-index-versioning](search-index-versioning.md)
- [runbooks/search-index-breaking-migration](../runbooks/search-index-breaking-migration.md)
- [song-internal-format](../songs/song-internal-format.md): the backup line format and `Properties` compression
- [data-layer](../infrastructure/data-layer.md): the SQL side of admin backup and restore
