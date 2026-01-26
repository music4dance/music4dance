# Usage Log Analysis Enhancement Plan

## Status: COMPLETE

## Overview

This document outlines enhancements to the UsageLog analysis capabilities in music4dance.net. The current implementation focuses on analyzing high-use customers (tracking guids with many hits). This plan extends functionality to also analyze:

1. **High-use pages** - URLs that are accessed frequently
2. **Low-use tracking guids** - UsageIds with few hits (potential one-time visitors or bots)

## Goals

1. **Page Analysis**: Identify which pages/URLs receive the most traffic
2. **Low-Usage Analysis**: Identify tracking guids with minimal activity
3. **Flexible URL Handling**: Support both full URLs (with query params) and base URLs
4. **No Database Changes**: Work within existing `UsageLog` table structure

---

## Current State

### Existing Model Classes (in `UsageLogController.cs`)

```csharp
public class UsageModel
{
    public DateTime LastUpdate { get; set; }
    public List<UsageSummary> Summaries { get; set; }
}
```

```csharp
// In m4dModels/UsageLog.cs
public class UsageSummary
{
    public string UsageId { get; set; }
    public string UserName { get; set; }
    public DateTimeOffset MinDate { get; set; }
    public DateTimeOffset MaxDate { get; set; }
    public int Hits { get; set; }
}
```

### Existing Controller Actions

| Action | Purpose |
|--------|---------|
| `Index()` | High-use tracking guids (>5 hits) |
| `DayLog(int days)` | Recent log entries |
| `UserLog(string user)` | Entries for a specific user |
| `IdLog(string usageId)` | Entries for a specific tracking guid |
| `ClearCache()` | Clears the cached model |

### Existing Views

- `Index.cshtml` - High-use tracking guid summary
- `DayLog.cshtml` - Day-based log view
- `UserLog.cshtml` - User-specific log view
- `IdLog.cshtml` - Tracking guid-specific log view
- `_UsageLog.cshtml` - Shared partial for log entry tables

---

## Implementation Plan

### Phase 1: New Model Classes

Add to `UsageLogController.cs`:

```csharp
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
}
```

### Phase 2: New Controller Actions

| Action | Purpose | SQL Approach |
|--------|---------|--------------|
| `Pages(bool useBaseUrl)` | High-use pages | GROUP BY Page, HAVING COUNT(*) > 10, with optional base URL extraction |
| `PageLog(string page)` | Detail log for specific page | WHERE Page = @page or LIKE @page% |
| `LowUsage()` | Low-use tracking guids | GROUP BY UsageId, HAVING COUNT(*) <= 5 |

**URL Handling Strategy:**

For base URL mode, use SQL to strip query parameters:
```sql
-- Full URL mode
SELECT [Page], COUNT(DISTINCT [UsageId]) as UniqueUsers, ...
FROM dbo.UsageLog 
GROUP BY [Page]

-- Base URL mode (strip query params)
SELECT 
    CASE 
        WHEN CHARINDEX('?', [Page]) > 0 
        THEN SUBSTRING([Page], 1, CHARINDEX('?', [Page]) - 1)
        ELSE [Page]
    END as Page,
    COUNT(DISTINCT [UsageId]) as UniqueUsers, ...
FROM dbo.UsageLog 
GROUP BY 
    CASE 
        WHEN CHARINDEX('?', [Page]) > 0 
        THEN SUBSTRING([Page], 1, CHARINDEX('?', [Page]) - 1)
        ELSE [Page]
    END
```

### Phase 3: New Views

| View | Description |
|------|-------------|
| `Pages.cshtml` | Table of high-use pages with hits, unique visitors, date range, toggle for base URL mode |
| `PageLog.cshtml` | Detail entries for a specific page (reuses `_UsageLog.cshtml` partial) |
| `LowUsage.cshtml` | Table of low-use tracking guids (similar to Index) |

### Phase 4: Update Index.cshtml

Add navigation section with links to:
- **Pages** - High-use pages analysis
- **Low Usage** - Low-use tracking guids

---

## Caching Strategy

| View | Caching | Rationale |
|------|---------|-----------|
| Index (high-use) | Static `s_model` | Stable data, expensive query |
| Pages (high-use) | Static `s_pageModel` | Stable data, expensive query |
| LowUsage | No cache | Transient data, changes frequently |

All caches cleared via `ClearCache()` action.

---

## UI Design

### Navigation Bar (added to Index.cshtml)

```
Views: [High Usage (current)] [Low Usage] [Pages]
```

### Pages View Toggle

```
URL Mode: [Full URL] [Base URL Only]
```

---

## File Changes Summary

| File | Change | Status |
|------|--------|--------|
| `m4d/Controllers/UsageLogController.cs` | Add PageSummary, PageUsageModel, Pages(), PageLog(), LowUsage() | ? Complete |
| `m4d/Views/UsageLog/Index.cshtml` | Add navigation links | ? Complete |
| `m4d/Views/UsageLog/Pages.cshtml` | New view for page analysis | ? Complete |
| `m4d/Views/UsageLog/PageLog.cshtml` | New view for page detail | ? Complete |
| `m4d/Views/UsageLog/LowUsage.cshtml` | New view for low-use tracking guids | ? Complete |

---

## Testing

1. Verify Pages view loads with reasonable performance
2. Verify base URL toggle correctly aggregates URLs
3. Verify LowUsage shows tracking guids with ?5 hits
4. Verify PageLog shows correct entries for selected page
5. Verify ClearCache clears both s_model and s_pageModel

