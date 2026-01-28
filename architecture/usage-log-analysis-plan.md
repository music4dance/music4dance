# Usage Log Analysis Architecture

## Status: COMPLETE

## Overview

This document describes the UsageLog analysis capabilities in music4dance.net. The system provides tools for analyzing user traffic patterns, identifying bot traffic, and understanding which pages receive the most engagement from real users.

## Goals

1. **Page Analysis**: Identify which pages/URLs receive the most traffic from real users
2. **Bot Filtering**: Separate bot/crawler traffic from human users
3. **User Engagement Analysis**: Distinguish single-hit visitors (often bots or bounces) from engaged multi-hit users
4. **Song Page Analysis**: Analyze `/song/details/{guid}` pages with enriched song metadata
5. **Flexible URL Handling**: Support both full URLs (with query params) and base URLs
6. **No Database Schema Changes**: Work within existing `UsageLog` table structure

---

## Data Model

### Enums

```csharp
public enum PageType
{
    All,        // All pages
    Songs,      // Only /song/details/* pages
    Other       // Everything except song detail pages
}

public enum UserHitFilter
{
    All,        // All users regardless of hit count
    SingleHit,  // Only users with exactly 1 total hit (landing page analysis)
    MultiHit    // Only users with >1 hits (engaged users)
}

public enum BotFilter
{
    All,         // All traffic including bots
    ExcludeBots, // Human traffic only (default)
    BotsOnly     // Only bot/crawler traffic
}
```

### View Models

```csharp
public class UsageModel
{
    public DateTime LastUpdate { get; set; }
    public List<UsageSummary> Summaries { get; set; }
    public BotFilter BotFilter { get; set; }
}

public class PageSummary
{
    public string Page { get; set; }
    public int UniqueUsers { get; set; }
    public DateTimeOffset MinDate { get; set; }
    public DateTimeOffset MaxDate { get; set; }
    public int Hits { get; set; }
}

public class PageUsageModel
{
    public DateTime LastUpdate { get; set; }
    public List<PageSummary> Summaries { get; set; }
    public bool UseBaseUrl { get; set; }
    public UserHitFilter UserHitFilter { get; set; }
    public PageType PageType { get; set; }
    public BotFilter BotFilter { get; set; }
}
```

---

## Controller Actions

| Action | Parameters | Purpose |
|--------|------------|---------|
| `Index(botFilter)` | `BotFilter` (default: ExcludeBots) | High-use tracking guids (>5 hits) |
| `Pages(useBaseUrl, userHitFilter, pageType, botFilter)` | Multiple filters | Page analysis with advanced filtering |
| `PageLog(page, exactMatch)` | Page URL, match mode | Detail log for specific page with song enrichment |
| `DayLog(days)` | Days offset | Recent log entries |
| `UserLog(user)` | Username | Entries for a specific user |
| `IdLog(usageId)` | Tracking GUID | Entries for a specific tracking guid |
| `ClearCache()` | None | Clears the cached Index model |

### Default Filter Values

The Pages view defaults to settings that focus on real user traffic:

| Filter | Default | Rationale |
|--------|---------|-----------|
| Page Type | Other Pages | Song details have their own analysis needs |
| URL Mode | Base URL Only | Aggregates query string variations |
| User Filter | Multi-Hit Users | Filters out single-visit bounces/bots |
| Bot Filter | Exclude Bots | Focuses on human traffic |

---

## Bot Detection

Bot traffic is identified by UserAgent patterns. The following patterns are filtered:

```sql
-- Bot detection patterns (case-insensitive LIKE)
'%bot%'
'%spider%'
'%crawler%'
'%slurp%'        -- Yahoo Slurp
'%Mediapartners%' -- Google Adsense
```

### Bot Filter SQL Fragments

```csharp
// ExcludeBots
AND [UserAgent] NOT LIKE '%bot%' 
AND [UserAgent] NOT LIKE '%spider%' 
AND [UserAgent] NOT LIKE '%crawler%'
AND [UserAgent] NOT LIKE '%slurp%'
AND [UserAgent] NOT LIKE '%Mediapartners%'

// BotsOnly
AND ([UserAgent] LIKE '%bot%' 
    OR [UserAgent] LIKE '%spider%' 
    OR [UserAgent] LIKE '%crawler%'
    OR [UserAgent] LIKE '%slurp%'
    OR [UserAgent] LIKE '%Mediapartners%')
```

---

## Song Page Enrichment

When drilling down to a specific page via `PageLog`, song details pages are enriched with song metadata:

1. URL is checked for `/song/details/` prefix
2. GUID is extracted from the URL path
3. Song is looked up via `SongIndex.FindSong()`
4. Title, Artist, and link to song are displayed in the page header

This enrichment happens only on the detail view (not the list) to avoid expensive lookups.

---

## File Structure

| File | Purpose |
|------|---------|
| `m4d/Controllers/UsageLogController.cs` | All enums, view models, and controller actions |
| `m4d/Views/UsageLog/Index.cshtml` | High Usage view with bot filter |
| `m4d/Views/UsageLog/Pages.cshtml` | Page analysis with all filters |
| `m4d/Views/UsageLog/PageLog.cshtml` | Detail view for specific page with song enrichment |
| `m4d/Views/UsageLog/DayLog.cshtml` | Recent entries view |
| `m4d/Views/UsageLog/UserLog.cshtml` | User-specific entries |
| `m4d/Views/UsageLog/IdLog.cshtml` | Tracking ID-specific entries |
| `m4d/Views/Shared/_UsageLog.cshtml` | Shared partial for log entry display |

---

## UI Design

### Navigation
```
Views: [High Usage] [Pages]
```

### Index (High Usage) View Filters
```
Bot Filter: [All Traffic] [Exclude Bots] [Bots Only]
```

### Pages View Filters
```
Page Type:   [All Pages] [Song Details] [Other Pages]
URL Mode:    [Full URL] [Base URL Only]
User Filter: [All Users] [Multi-Hit Users] [Single-Hit Users]
Bot Filter:  [All Traffic] [Exclude Bots] [Bots Only]
```

### PageLog Header (for song pages)
```
Page: /song/details/abc-123...
Song: Song Title (link) by Artist Name
Match Mode: Exact | Starts With
Count: 123
```

---

## Caching Strategy

| View | Caching | Condition | Rationale |
|------|---------|-----------|-----------|
| Index | Static `s_model` | Only when `botFilter == ExcludeBots` | Default view is cached; other filters bypass cache |
| Pages | No cache | N/A | Multiple filter combinations make caching complex |
| PageLog | No cache | N/A | Detail view, infrequent access |

### Cache Invalidation

- `ClearCache()` action clears the Index cache
- Cache is only populated when accessing Index with default (ExcludeBots) filter
- Non-default filter values always execute fresh queries

---

## Performance: Recommended Indexes

**Note:** These indexes are recommended for **local development only**. Adding indexes improves query performance but slows down INSERT operations. Since UsageLog data is typically pulled from production and analyzed locally, indexing the cloud database may not be necessary.

### Manual SQL Index Creation

Run these in SSMS against your LocalDB:

```sql
-- Connect to: (localdb)\MSSQLLocalDB
-- Database: m4d (or your database name)

-- Drop existing indexes first (if they exist)
DROP INDEX IF EXISTS IX_UsageLog_UsageId ON dbo.UsageLog;
DROP INDEX IF EXISTS IX_UsageLog_Page ON dbo.UsageLog;
DROP INDEX IF EXISTS IX_UsageLog_UserName ON dbo.UsageLog;
DROP INDEX IF EXISTS IX_UsageLog_Date ON dbo.UsageLog;
GO

-- Primary index for high-use users query (Index page, GROUP BY UsageId)
-- Also supports IdLog and user hit filter subqueries
CREATE NONCLUSTERED INDEX IX_UsageLog_UsageId 
ON dbo.UsageLog ([UsageId]) 
INCLUDE ([UserName], [Date], [Page]);
GO

-- Index for Pages query (GROUP BY Page with various filters)
CREATE NONCLUSTERED INDEX IX_UsageLog_Page 
ON dbo.UsageLog ([Page]) 
INCLUDE ([UsageId], [Date]);
GO

-- Index for UserLog query (WHERE UserName = @user ORDER BY Id DESC)
CREATE NONCLUSTERED INDEX IX_UsageLog_UserName 
ON dbo.UsageLog ([UserName], [Id] DESC);
GO

-- Index for DayLog query (WHERE Date < @date ORDER BY Id DESC)
CREATE NONCLUSTERED INDEX IX_UsageLog_Date 
ON dbo.UsageLog ([Date] DESC, [Id] DESC);
GO

-- Verify indexes were created
SELECT 
    i.name AS IndexName,
    i.type_desc AS IndexType,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns
FROM sys.indexes i
JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID('dbo.UsageLog')
  AND i.name LIKE 'IX_UsageLog_%'
GROUP BY i.name, i.type_desc;
```

### EF Core Migration (if deploying to cloud)

If you decide to add these indexes to the production database, create a migration:

```csharp
public partial class AddUsageLogIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Index for GROUP BY UsageId queries
        migrationBuilder.CreateIndex(
            name: "IX_UsageLog_UsageId",
            table: "UsageLog",
            column: "UsageId")
            .Annotation("SqlServer:Include", new[] { "UserName", "Date", "Page" });

        // Index for GROUP BY Page queries
        migrationBuilder.CreateIndex(
            name: "IX_UsageLog_Page",
            table: "UsageLog",
            column: "Page")
            .Annotation("SqlServer:Include", new[] { "UsageId", "Date" });

        // Index for UserLog queries
        migrationBuilder.CreateIndex(
            name: "IX_UsageLog_UserName",
            table: "UsageLog",
            columns: new[] { "UserName", "Id" });

        // Index for DayLog queries
        migrationBuilder.CreateIndex(
            name: "IX_UsageLog_Date",
            table: "UsageLog",
            columns: new[] { "Date", "Id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_UsageLog_UsageId", table: "UsageLog");
        migrationBuilder.DropIndex(name: "IX_UsageLog_Page", table: "UsageLog");
        migrationBuilder.DropIndex(name: "IX_UsageLog_UserName", table: "UsageLog");
        migrationBuilder.DropIndex(name: "IX_UsageLog_Date", table: "UsageLog");
    }
}
```

**Note:** The `SqlServer:Include` annotation for included columns may require EF Core 7.0+. For earlier versions, use raw SQL in the migration.

### Index-to-Query Mapping

| Index | Supports | Query Pattern |
|-------|----------|---------------|
| `IX_UsageLog_UsageId` | Index page, IdLog, user hit filter subquery | `GROUP BY UsageId`, `WHERE UsageId = @id` |
| `IX_UsageLog_Page` | Pages view, PageLog | `GROUP BY Page`, `WHERE Page LIKE '/song/details/%'` |
| `IX_UsageLog_UserName` | UserLog | `WHERE UserName = @user ORDER BY Id DESC` |
| `IX_UsageLog_Date` | DayLog | `WHERE Date < @date ORDER BY Id DESC` |

---

## Testing Checklist

1. ? Index view loads with bot filter working
2. ? Pages view loads with all filter combinations
3. ? Page type filter correctly separates song pages from others
4. ? User hit filter distinguishes single-hit vs multi-hit users
5. ? Bot filter excludes known crawler traffic
6. ? Base URL toggle correctly aggregates query string variations
7. ? PageLog shows song title/artist for song detail pages
8. ? Cache works correctly (only caches default Index view)
9. ? Clear Cache invalidates the cached model

---

## Key Insights from Analysis

### Bot Traffic Patterns

- URLs like `/song/album`, `/song/index`, `/song/artist` without parameters are typically bot probes (these URLs generate errors for real users)
- High hit count with `Hits == UniqueUsers` indicates bot traffic (each bot hits once)
- Filtering by "Multi-Hit Users" + "Exclude Bots" reveals real engaged users

### Useful Filter Combinations

| Analysis Goal | Page Type | URL Mode | User Filter | Bot Filter |
|---------------|-----------|----------|-------------|------------|
| Real user landing pages | Other | Base URL | Single-Hit | Exclude Bots |
| Popular pages (engaged users) | Other | Base URL | Multi-Hit | Exclude Bots |
| Most viewed songs | Songs | Base URL | Multi-Hit | Exclude Bots |
| Bot crawling patterns | All | Full URL | All | Bots Only |



