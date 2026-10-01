# Phase 6: Azure Search Credential Error Handling - Completion Report

**Date**: January 14, 2026
**Status**: ✅ COMPLETE
**Branch**: self-contained-dotnet10

## Overview

Phase 6 extended the service resilience implementation to properly handle Azure Search credential errors that occur when the Azure SDK cannot create search clients. This phase focused on fixing error propagation from the lowest levels of the search stack through to user-facing interfaces.

## Problem Statement

When Azure Search clients are registered without proper `TokenCredential`, the Azure SDK throws `InvalidOperationException: "Client registration requires a TokenCredential"`. This error was:

1. **Not caught during startup**: Azure SDK uses lazy initialization - clients aren't created until first use
2. **Swallowed by generic catch blocks**: Exception handling was too broad and prevented proper error propagation
3. **Not marking service unavailable**: Errors didn't trigger the service health tracking system
4. **Causing application crashes**: Unhandled exceptions reached the user instead of graceful degradation

## Root Causes Discovered

Through systematic debugging, we identified three separate issues preventing proper error handling:

### Issue 1: Premature Health Marking

**Location**: [m4d/Program.cs](c:\Projects\music4dance\m4d\Program.cs) line 278

**Problem**: Code marked SearchService as "Healthy" immediately after client registration:

```csharp
serviceHealth.MarkHealthy("SearchService");  // ❌ Too early - SDK hasn't validated credentials yet
```

**Root Cause**: Azure SDK uses lazy initialization. Client registration succeeds even with invalid credentials - actual validation only happens on first search operation.

**Solution**: Removed premature health marking. Added comment explaining deferred validation.

### Issue 2: Pessimistic Unknown Service Handling

**Location**: [m4d/Services/ServiceHealth/ServiceHealthManager.cs](c:\Projects\music4dance\m4d\Services\ServiceHealth\ServiceHealthManager.cs)

**Problem**: `IsServiceHealthy()` returned `false` for services in `Unknown` state:

```csharp
return status.Status == ServiceStatus.Healthy;  // ❌ Returns false for Unknown
```

**Root Cause**: First-use discovery required optimistic handling - we need to attempt the operation to discover if credentials are valid.

**Solution**: Changed to optimistic approach:

```csharp
// Optimistically treat unknown services as healthy to allow first-use discovery
return status.Status != ServiceStatus.Unavailable;  // ✅ Only false if explicitly unavailable
```

### Issue 3: Exception Swallowing in Search Layer

**Location**: [m4dModels/SongIndex.cs](c:\Projects\music4dance\m4dModels\SongIndex.cs) line 1023-1044

**Problem**: Generic `catch (Exception)` block was swallowing `InvalidOperationException`:

```csharp
catch (Exception e)
{
    Log.Warn($"Azure Search exception: {e.Message}");
    return new SearchResults(...);  // ❌ Returns empty results without notifying upper layers
}
```

**Root Cause**: Generic exception handler caught ALL exceptions including the credential error, preventing `SongSearch.cs` from detecting the failure and marking service unavailable.

**Solution**: Added specific catch for `InvalidOperationException` that re-throws:

```csharp
catch (InvalidOperationException)
{
    // Re-throw service unavailability - let upper layers handle credential errors
    throw;  // ✅ Propagates to SongSearch which marks service unavailable
}
catch (Exception e)
{
    // Still catch other unexpected errors
    Log.Warn($"Azure Search exception: {e.Message}");
    return new SearchResults(...);
}
```

## Implementation Changes

### 1. Error Detection and Propagation

**SongIndex.cs** (Low-level Azure Search wrapper):

- `DoSearch()` - Catches TokenCredential errors, throws descriptive InvalidOperationException
- `FindSong()` - Catches TokenCredential errors, throws descriptive InvalidOperationException
- `Search()` - Re-throws InvalidOperationException to allow upper layers to handle

**SongSearch.cs** (Business logic layer):

- `Search()` - Catches InvalidOperationException, marks service unavailable, returns empty results
- `VoteSearch()` - Catches InvalidOperationException, marks service unavailable, returns empty results

### 2. Controller Fail-Fast Pattern

All search entry points now check service health **before** attempting operations:

```csharp
if (!IsSearchAvailable())
{
    return View("Error", new ErrorViewModel {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
        Message = "Search service is currently unavailable. Please try again later."
    });
}
```

**Controllers Updated:**

- **SongController.cs**: Index, Search, NewMusic, Details, Sort, FilterUser, Tags, AddTags, RemoveTags, Delete, UpdateSongAndServices (11 methods)
- **CustomSearchController.cs**: Holiday/themed search pages
- **API Controllers**: SongController, MusicServiceController, SpotifyPlaylistController, ServiceTrackController

All API controllers return HTTP 503 with clear error messages when search unavailable.

### 3. Service Health Management

**ServiceHealthManager.cs**:

- `IsServiceHealthy()` - Now optimistic about unknown services (returns true unless explicitly unavailable)
- Allows first-use discovery of credential errors
- Only marks service unavailable after actual failure

**Program.cs**:

- Removed premature `MarkHealthy("SearchService")` call
- Added comment explaining lazy validation approach
- Service health discovered on first search attempt

### 4. Error Responses

**Empty Results Constructor**:

```csharp
// Properly constructed empty SearchResults when service unavailable
return new SearchResults(
    Filter.SearchString ?? "",  // Query
    0,                          // Count
    0,                          // TotalCount
    1,                          // CurrentPage
    PageSize ?? 25,            // PageSize (with null coalescing)
    [],                        // Empty songs list
    new Dictionary<string, IList<Azure.Search.Documents.Models.FacetResult>>()  // Empty facets
);
```

Fixed `NullReferenceException` that occurred when passing `null` to SearchResults copy constructor.

## Error Propagation Chain

The complete flow when credential error occurs:

1. **First Request**: User navigates to `/songs` or performs search
2. **Health Check**: `IsSearchAvailable()` returns `true` (service is Unknown, not Unavailable)
3. **Attempt Search**: `SongSearch.Search()` calls `SongIndex.Search()`
4. **Azure SDK**: Attempts to create client, throws `InvalidOperationException: "Client registration requires a TokenCredential"`
5. **DoSearch()**: Catches error, re-throws as `InvalidOperationException: "Azure Search service is unavailable"`
6. **Search()**: Re-throws the InvalidOperationException (no longer swallows it)
7. **SongSearch**: Catches error, calls `ServiceHealth.MarkUnavailable("SearchService")`, returns empty results
8. **Service Marked**: SearchService now in Unavailable state
9. **Client Notified**: `/api/health/status` endpoint reflects unavailable state, menuContext shows banner
10. **Subsequent Requests**: `IsSearchAvailable()` returns `false`, operations fail fast without retrying

## Testing Validation

### Manual Testing Performed:

1. ✅ Removed TokenCredential configuration
2. ✅ Started application - no crashes, startup succeeded
3. ✅ First search request triggered error detection
4. ✅ Service marked as unavailable
5. ✅ Banner displayed on frontend (via menuContext)
6. ✅ Subsequent requests failed fast without retry attempts
7. ✅ Health endpoint showed correct unavailable status

### Verification Points:

- ✅ No application crashes
- ✅ Service unavailable status persists across requests
- ✅ User sees friendly error message
- ✅ Other site features remain functional
- ✅ Error logged with full details for debugging

## Files Modified

**Core Service Health:**

- `m4d/Services/ServiceHealth/ServiceHealthManager.cs` - Optimistic unknown handling
- `m4d/Program.cs` - Removed premature health marking

**Search Layer:**

- `m4dModels/SongIndex.cs` - Fixed exception swallowing, added credential error detection
- `m4d/Services/SongSearch.cs` - Added error catching and service health marking, fixed empty results construction

**Controllers (15+ methods updated):**

- `m4d/Controllers/SongController.cs` - Fail-fast checks and error handling
- `m4d/Controllers/CustomSearchController.cs` - Error handling
- `m4d/APIControllers/SongController.cs` - HTTP 503 responses
- `m4d/APIControllers/MusicServiceController.cs` - HTTP 503 responses
- `m4d/APIControllers/SpotifyPlaylistController.cs` - HTTP 503 responses
- `m4d/APIControllers/ServiceTrackController.cs` - HTTP 503 responses

**Base Controller:**

- `m4d/Controllers/ResilientController.cs` - Simplified health check methods (removed ViewData pattern in favor of menuContext)

**Cleanup:**

- `m4d/Services/StartupInitializationService.cs` - Removed unnecessary `#nullable enable`

## Architecture Decisions

### ViewData vs. MenuContext for Service Status

**Initial Approach** (Removed): Used ViewData to communicate service status to views:

```csharp
ViewData["ShowSearchUnavailableNotice"] = true;  // ❌ Removed
```

**Final Approach**: Service status communicated via menuContext API:

- Frontend polls `/api/health/status` periodically
- Vue composable manages service health state
- Status banner rendered based on API data
- More scalable for SPA architecture
- Single source of truth for all pages

**Rationale**: menuContext approach aligns with existing architecture patterns and works better for Vue.js frontend.

### Nullable Annotations

**Decision**: Did NOT enable `#nullable` project-wide. Only new service health files use nullable reference types:

- `ServiceHealthManager.cs`
- `ServiceStatus.cs`
- `ServiceHealthNotifier.cs`
- `NullSearchClientFactories.cs`
- `ResilientController.cs`
- `HealthController.cs`

**Rationale**: Project-wide nullable migration is a separate, larger effort. Service health implementation kept self-contained.

## Benefits Achieved

1. **No Application Crashes**: Credential errors handled gracefully at all levels
2. **Clear User Communication**: Banner appears immediately on first failure
3. **Fail-Fast Performance**: Subsequent requests don't retry failed operations
4. **Comprehensive Logging**: All credential errors logged with full context
5. **Diagnostic Visibility**: Health endpoints show exact error state for operations teams
6. **Service Isolation**: Search failure doesn't affect other site features

## Next Steps

This completes the Azure Search resilience implementation. Future enhancements could include:

1. **Automatic Recovery**: Periodic background checks to detect when credentials are restored
2. **Retry Logic**: Exponential backoff for transient errors (as opposed to permanent credential errors)
3. **SQL Server Resilience**: Apply similar patterns to database credential errors
4. **Configuration Validation**: Pre-startup validation of all credential configurations (if feasible)

## Conclusion

Phase 6 successfully implemented comprehensive error handling for Azure Search credential failures. The system now properly detects, propagates, and responds to authentication errors throughout the entire stack. Users receive clear feedback, the site remains functional, and operations teams have full visibility into service health status.
