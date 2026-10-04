# Analyze Usage Logs

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-01

## When to use

Answering "what are real visitors doing?": popular pages, landing pages, most-viewed songs,
crawler patterns. How the data is recorded, and what the pages show, is in
[usage-tracking](../observability/usage-tracking.md#analysis-pages).

## Prerequisites / access

- A `showDiagnostics` account. The pages are under `/UsageLog/`.
- For heavy analysis, a **local** copy of the data is better. The analysis queries `GROUP BY` over
  the whole `UsageLog` table, so run them against LocalDB, with the extra indexes below added
  locally only. In production, those indexes would slow every insert.

## Saving and loading usage data

Usage analysis is normally run locally against a copy of the production `UsageLog` table. The
round trip is: export the table from production to a TSV file, then load that file into the local
database through the admin page.

### Saving (export from production)

There is no export endpoint in the app; the export is done directly against the production
database.

> **TODO:** document the export procedure (tool, query and output settings).

Whatever tool is used, the output must match the file format below.

### File format

- Tab-separated, UTF-8, one row per `UsageLog` record
- First row is a header whose column names match the `UsageLog` properties
  ([UsageLog.cs](../../m4dModels/UsageLog.cs)): `Id`, `UsageId`, `UserName`, `Date`, `Page`,
  `Query`, `Filter`, `Referrer`, `UserAgent`
- `Id` values in the file are ignored — the loader resets each to 0 so the local table assigns
  fresh identity values
- Read by CsvHelper with `Delimiter = "	"` and `CsvMode.NoEscape` — fields are **not** quoted or
  escaped, so a tab or newline inside a value will break the row

### Loading (import into a local database)

Both loaders live on the admin **Upload Backup** page
([UploadBackup.cshtml](../../m4d/Views/Admin/UploadBackup.cshtml), "Load Usage Data" section),
require the `dbAdmin` role, and are hidden when the environment is Production. Both insert in
batches (default 5,000, allowed range 100–50,000), clearing the EF change tracker between batches
to keep memory flat. Progress shows in the admin task monitor.

| Option                 | Action                                  | Use when                                       | Existing rows        |
| ---------------------- | --------------------------------------- | ---------------------------------------------- | -------------------- |
| **Load (Incremental)** | `Admin/LoadUsage` (browser file upload) | Small files, appending a newer slice           | Kept (appended)      |
| **Load from AppData**  | `Admin/LoadUsageFromAppData`            | Full reloads; files too large to upload (>2GB) | Truncated by default |

**Load from AppData steps:**

1. Copy the TSV into `m4d/wwwroot/AppData/` (created on demand by `EnsureAppData`). The default
   file name is `usage.tsv`; any plain file name in that folder is accepted.
2. On the Upload Backup page, set the file name and batch size, and leave **Truncate table first**
   checked for a full reload (it runs `TRUNCATE TABLE UsageLog`).
3. Submit and watch the task monitor for the final record count.
4. Clear the usage report cache (`UsageLog/ClearCache`) so the Index view picks up the new data.

**Caveats:**

- `dotnet clean` runs `m4d`'s `clean-client` target, which deletes all of `wwwroot` — including
  `wwwroot/AppData`. Re-copy the file after a clean.
- The incremental loader does not de-duplicate. Loading a file that overlaps existing rows
  produces duplicates; use a full truncate-and-load instead when in doubt.
- For a large local table, add the [local performance indexes](#local-performance-indexes) after
  loading, not before — inserts are much faster without them.

## Steps

1. Open **`/UsageLog/Pages`** and pick a filter combination from the table under "Key insights"
   below. The defaults (other pages, base URL, multi-hit users, bots excluded) answer "what do
   engaged real users look at?"
2. Drill into a page with **`PageLog`**. For `/song/details/{id}` pages, the header shows the
   song's title and artist.
3. For a single visitor or account, use **`IdLog(usageId)`** or **`UserLog(user)`**. Use
   **`DayLog(days)`** for recent raw rows.
4. **`Index`** lists high-use tracking IDs (more than 5 hits). Its default view is cached; use
   **`ClearCache`** to refresh it.

## Local performance indexes

Run these against your **local** database only.

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

## Key insights

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
