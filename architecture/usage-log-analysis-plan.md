# Usage Log Analysis Enhancement Plan

## Status: COMPLETE (Revised)

## Overview

This document outlines enhancements to the UsageLog analysis capabilities in music4dance.net. The current implementation focuses on analyzing high-use customers (tracking guids with many hits). This plan extends functionality to analyze page traffic with advanced filtering.

## Goals

1. **Page Analysis**: Identify which pages/URLs receive the most traffic
2. **Single-Hit User Analysis**: Filter pages to show only traffic from users with a single hit (landing page analysis)
3. **Song Page Separation**: Analyze `/song/details/{guid}` pages separately with enriched song metadata
4. **Flexible URL Handling**: Support both full URLs (with query params) and base URLs
5. **No Database Changes**: Work within existing `UsageLog` table structure

---

## Implemented Features

### Model Classes (in `UsageLogController.cs`)

```csharp
public class PageSummary
{
    public string Page { get; set; }
    public int UniqueUsers { get; set; }
    public DateTimeOffset MinDate { get; set; }
    public DateTimeOffset MaxDate { get; set; }
    public int Hits { get; set; }
    public string? SongTitle { get; set; }  // Enriched for song pages
    public string? SongArtist { get; set; } // Enriched for song pages
}

public enum PageType { All, Songs, Other }

public class PageUsageModel
{
    public DateTime LastUpdate { get; set; }
    public List<PageSummary> Summaries { get; set; }
    public bool UseBaseUrl { get; set; }
    public bool SingleHitOnly { get; set; }
    public PageType PageType { get; set; }
}
```

### Controller Actions

| Action | Purpose |
|--------|---------|
| `Index()` | High-use tracking guids (>5 hits) |
| `Pages(useBaseUrl, singleHitOnly, pageType)` | Page analysis with filters |
| `PageLog(page, exactMatch)` | Detail log for specific page |
| `DayLog(days)` | Recent log entries |
| `UserLog(user)` | Entries for a specific user |
| `IdLog(usageId)` | Entries for a specific tracking guid |
| `ClearCache()` | Clears the cached model |

### Pages View Filters

1. **Page Type**: All Pages | Song Details | Other Pages
2. **URL Mode**: Full URL | Base URL Only
3. **User Filter**: All Users | Single-Hit Users Only

### Song Page Enrichment

When viewing "Song Details" page type, the system:
- Extracts the GUID from `/song/details/{guid}` URLs
- Looks up the song via `SongIndex.FindSong()`
- Displays song title and artist in additional columns

---

## File Changes Summary

| File | Change | Status |
|------|--------|--------|
| `m4d/Controllers/UsageLogController.cs` | PageSummary with song fields, PageType enum, enhanced Pages() | ? Complete |
| `m4d/Views/UsageLog/Index.cshtml` | Simplified navigation (removed Low Usage) | ? Complete |
| `m4d/Views/UsageLog/Pages.cshtml` | Filter controls, song title/artist columns, fixed widths | ? Complete |
| `m4d/Views/UsageLog/PageLog.cshtml` | Page detail view | ? Complete |
| `m4d/Views/UsageLog/LowUsage.cshtml` | Removed (replaced by single-hit filter) | ? Removed |

---

## UI Design

### Navigation
```
Views: [High Usage] [Pages]
```

### Pages View Filters
```
Page Type:  [All Pages] [Song Details] [Other Pages]
URL Mode:   [Full URL] [Base URL Only]
User Filter: [All Users] [Single-Hit Users Only]
```

### Column Widths (Pages View)
- Hits: 5em
- Users: 5em
- Song Title: 15em (only shown for Song Details)
- Artist: 10em (only shown for Song Details)
- Days: 3em

---

## Caching Strategy

| View | Caching | Rationale |
|------|---------|-----------|
| Index (high-use users) | Static `s_model` | Stable data, expensive query |
| Pages | No cache | Multiple filter combinations make caching complex |


---

## Testing

1. Verify Pages view loads with reasonable performance
2. Verify page type filter correctly separates song pages from others
3. Verify single-hit user filter shows only landing pages
4. Verify song title/artist display when viewing Song Details
5. Verify base URL toggle correctly aggregates URLs

---

## Performance: Recommended Indexes

**Note:** These indexes are recommended for **local development only**. Adding indexes improves query performance but slows down INSERT operations. Since UsageLog data is typically pulled from production and analyzed locally, indexing the cloud database may not be necessary.

### Index Definitions

```sql
-- Primary index for high-use users query (Index page, GROUP BY UsageId)
-- Also supports IdLog and single-hit user subquery
CREATE NONCLUSTERED INDEX IX_UsageLog_UsageId 
ON dbo.UsageLog ([UsageId]) 
INCLUDE ([UserName], [Date], [Page]);

-- Index for Pages query (GROUP BY Page with various filters)
CREATE NONCLUSTERED INDEX IX_UsageLog_Page 
ON dbo.UsageLog ([Page]) 
INCLUDE ([UsageId], [Date]);

-- Index for UserLog query (WHERE UserName = @user ORDER BY Id DESC)
CREATE NONCLUSTERED INDEX IX_UsageLog_UserName 
ON dbo.UsageLog ([UserName], [Id] DESC);

-- Index for DayLog query (WHERE Date < @date ORDER BY Id DESC)
CREATE NONCLUSTERED INDEX IX_UsageLog_Date 
ON dbo.UsageLog ([Date] DESC, [Id] DESC);
```

### Index-to-Query Mapping

| Index | Supports | Query Pattern |
|-------|----------|---------------|
| `IX_UsageLog_UsageId` | Index page, IdLog, single-hit subquery | `GROUP BY UsageId`, `WHERE UsageId = @id` |
| `IX_UsageLog_Page` | Pages view, PageLog | `GROUP BY Page`, `WHERE Page LIKE '/song/details/%'` |
| `IX_UsageLog_UserName` | UserLog | `WHERE UserName = @user ORDER BY Id DESC` |
| `IX_UsageLog_Date` | DayLog | `WHERE Date < @date ORDER BY Id DESC` |

### To Apply Locally

Run in SSMS against your LocalDB:
```
Server: (localdb)\MSSQLLocalDB
Database: m4d (or your database name)
```

### To Remove (if needed)

```sql
DROP INDEX IF EXISTS IX_UsageLog_UsageId ON dbo.UsageLog;
DROP INDEX IF EXISTS IX_UsageLog_Page ON dbo.UsageLog;
DROP INDEX IF EXISTS IX_UsageLog_UserName ON dbo.UsageLog;
DROP INDEX IF EXISTS IX_UsageLog_Date ON dbo.UsageLog;
```


