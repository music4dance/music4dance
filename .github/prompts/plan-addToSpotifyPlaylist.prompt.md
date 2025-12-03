# Add to Spotify Playlist Feature Implementation Plan

**Issue**: [#82 - Add to Spotify Playlist](https://github.com/music4dance/music4dance/issues/82)
**Created**: 2025-11-28
**Status**: Planning

## Overview

Enable users to add individual songs to their Spotify playlists directly from song pages and song lists, complementing the existing "Create Spotify Playlist" feature that exports entire search results.

### Key Design Decisions

1. **Client-side playlist caching**: Cache user's Spotify playlists on first button click, refresh available via UI
2. **No batch operations**: Single-song additions only (future enhancement possibility)
3. **Authentication flow**: Use existing login redirect pattern with Spotify account association check
4. **Premium-only feature**: Restrict to premium subscribers but show to all users for marketing
5. **Code reuse**: Leverage logic from existing `CreateSpotify` implementation where possible

## Technical Architecture

### Backend Components

#### 1. New API Controller: `SpotifyPlaylistController.cs`

**Location**: `m4d/APIControllers/SpotifyPlaylistController.cs`

**Endpoints**:

```csharp
[ApiController]
[Route("api/spotify/playlist")]
[Authorize]
[ValidateAntiForgeryToken]
public class SpotifyPlaylistController : DanceMusicApiController
{
    // GET api/spotify/playlist/user
    // Returns list of user's Spotify playlists
    [HttpGet("user")]
    public async Task<ActionResult<IEnumerable<PlaylistMetadata>>> GetUserPlaylists()

    // POST api/spotify/playlist/add
    // Adds song's Spotify track to specified playlist
    [HttpPost("add")]
    public async Task<ActionResult<AddToPlaylistResult>> AddTrackToPlaylist(
        [FromBody] AddToPlaylistRequest request)
}
```

**Implementation Details**:

- Use `AdmAuthentication.HasAccess()` to verify Spotify OAuth (pattern from `SongController.CanSpotify()`)
- Check premium status: `User.IsInRole(DanceMusicCoreService.PremiumRole) || User.IsInRole(DanceMusicCoreService.TrialRole)`
- Return HTTP 402 (Payment Required) for non-premium users with marketing message
- Return HTTP 401 for unauthenticated users
- Use `MusicServiceManager.GetUserPlaylists()` to fetch playlists
- Use `MusicServiceManager.AddToPlaylist()` to add track (existing method from create playlist feature)
- Activity logging via `ActivityLog` if `FeatureFlags.ActivityLogging` enabled

**Models** (in `m4dModels`):

```csharp
public class AddToPlaylistRequest
{
    public string SongId { get; set; }        // m4d song ID
    public string PlaylistId { get; set; }    // Spotify playlist ID
}

public class AddToPlaylistResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string SnapshotId { get; set; }    // Spotify playlist snapshot
}
```

#### 2. Music Service Manager Enhancement

**Location**: `m4dModels/MusicServiceManager.cs`

**New Method** (if not exists):

```csharp
public static async Task<List<PlaylistMetadata>> GetUserPlaylists(
    MusicService service,
    ClaimsPrincipal user)
```

- Uses Spotify API: `GET https://api.spotify.com/v1/me/playlists`
- Returns playlist metadata (id, name, description, track count, images)
- Filters to only user's owned/editable playlists

### Frontend Components

#### 1. Core Component: `AddToPlaylistButton.vue`

**Location**: `m4d/ClientApp/src/components/AddToPlaylistButton.vue`

**Props**:

```typescript
interface Props {
  song: Song; // Song to add
  variant?: string; // Button variant (default: "outline-primary")
  size?: string; // Button size (default: "sm")
  showText?: boolean; // Show button text vs icon-only (default: true)
}
```

**State Management**:

```typescript
const playlists = ref<PlaylistMetadata[]>([]);
const loading = ref(false);
const showDropdown = ref(false);
const error = ref<string | null>(null);
const playlistsCached = ref(false);
```

**Key Methods**:

- `fetchPlaylists()`: Lazy-load playlists on first click, cache client-side
- `addToPlaylist(playlistId)`: Call backend API, show toast notification
- `handleAuthError()`: Redirect to login with return URL
- `handlePremiumError()`: Show modal encouraging upgrade
- `handleSpotifyError()`: Redirect to external logins page

**UI Pattern**:

```vue
<BDropdown>
  <template #button-content>
    <IBiSpotify /> Add to Playlist
  </template>
  <BDropdownItem v-for="playlist in playlists" @click="addToPlaylist(playlist.id)">
    {{ playlist.name }}
  </BDropdownItem>
  <BDropdownDivider />
  <BDropdownItem @click="refreshPlaylists">
    <IBiArrowClockwise /> Refresh Playlists
  </BDropdownItem>
</BDropdown>
```

**Error Handling**:

- 401 Unauthorized → Redirect to `/identity/account/login?returnUrl={current}`
- 402 Payment Required → Show premium upgrade modal with link to `/home/contribute`
- 403 Forbidden (no Spotify) → Redirect to `/identity/account/manage/externallogins?returnUrl={current}`
- 404 Not Found (track) → Toast: "This song is not available on Spotify"
- 500 Server Error → Toast: "Unable to add to playlist. Please try again."

**Toast Notifications**:

- Success: "Added '{song.title}' to '{playlist.name}'"
- Error: Specific error message based on response

#### 2. Premium Gate Modal: `PremiumRequiredModal.vue`

**Location**: `m4d/ClientApp/src/components/PremiumRequiredModal.vue`

**Props**:

```typescript
interface Props {
  featureName: string; // "Add to Spotify Playlist"
}
```

**Content** (based on `RequiresPremium.cshtml`):

- Marketing message about premium benefits
- Link to `/home/contribute` subscription page
- Link to feature documentation on blog
- Close/dismiss option

#### 3. Integration Points

##### A. PlayModal Enhancement

**File**: `m4d/ClientApp/src/components/PlayModal.vue`

**Changes**:

```vue
<template>
  <BModal>
    <!-- Existing purchase links -->
    <div class="purchase-services">
      <!-- existing PurchaseService links -->
    </div>

    <!-- NEW: Add to Playlist button -->
    <div v-if="hasSpotifyTrack" class="my-3">
      <AddToPlaylistButton :song="song" :show-text="true" />
    </div>

    <!-- Existing audio player -->
    <SamplePlayer />
  </BModal>
</template>

<script setup lang="ts">
const hasSpotifyTrack = computed(() => {
  return !!song.value.getServiceTrack(ServiceType.Spotify);
});
</script>
```

##### B. SongDetails Page Enhancement

**File**: `m4d/ClientApp/src/pages/song-details/components/PurchaseSection.vue`

**Changes**:

```vue
<template>
  <div class="purchase-section">
    <!-- Existing purchase charms/logos -->
    <div class="service-logos">
      <a v-for="service in purchaseServices">
        <!-- existing logo display -->
      </a>
    </div>

    <!-- NEW: Add to Playlist button -->
    <div v-if="hasSpotifyTrack" class="mt-2">
      <AddToPlaylistButton :song="song" :variant="'primary'" :size="'md'" />
    </div>
  </div>
</template>
```

##### C. Optional: SongTable Enhancement (Future)

**File**: `m4d/ClientApp/src/components/SongTable.vue`

**Note**: Deferred for future implementation. Would add icon to PlayCell component.

### Authentication & Authorization Flow

#### User States & Handling

| State                | Check                          | Action                                                                | UI Feedback                       |
| -------------------- | ------------------------------ | --------------------------------------------------------------------- | --------------------------------- |
| **Not logged in**    | `!menuContext.isAuthenticated` | Redirect to `/identity/account/login?returnUrl={url}`                 | Show login prompt                 |
| **Not premium**      | `!menuContext.isPremium`       | Show `PremiumRequiredModal`                                           | Marketing modal with upgrade link |
| **No Spotify login** | Backend returns 403            | Redirect to `/identity/account/manage/externallogins?returnUrl={url}` | Link to external logins           |
| **All checks pass**  | Backend returns 200            | Execute add to playlist                                               | Success toast                     |

#### Backend Validation Sequence

```csharp
public async Task<ActionResult> AddTrackToPlaylist(AddToPlaylistRequest request)
{
    // 1. Authentication (handled by [Authorize] attribute)
    // Returns 401 if not authenticated

    // 2. Premium check
    if (!IsPremium())
    {
        return StatusCode(402, new {
            message = "Premium subscription required",
            upgradeUrl = "/home/contribute"
        });
    }

    // 3. Spotify OAuth check
    if (!await CanSpotify())
    {
        return StatusCode(403, new {
            message = "Spotify account not connected",
            connectUrl = "/identity/account/manage/externallogins"
        });
    }

    // 4. Get song and validate Spotify track exists
    var song = await Database.FindSong(request.SongId);
    var spotifyId = song?.GetPurchaseId(ServiceType.Spotify);
    if (spotifyId == null)
    {
        return NotFound(new { message = "Song not available on Spotify" });
    }

    // 5. Execute add to playlist
    // ...
}
```

### Data Flow Diagram

```
User clicks "Add to Playlist" button
    ↓
Component checks authentication state
    ↓ (if not auth)
    → Redirect to login page with returnUrl

    ↓ (if authenticated)
Check if playlists cached
    ↓ (if not cached)
    → GET /api/spotify/playlist/user
    ↓ (API checks premium + Spotify OAuth)
    → Return playlists or error
    → Cache playlists in component state

    ↓ (playlists available)
Show dropdown with playlist options
    ↓
User selects playlist
    ↓
POST /api/spotify/playlist/add { songId, playlistId }
    ↓ (API validates all requirements)
    → MusicServiceManager.AddToPlaylist()
    → Spotify API: POST /v1/playlists/{id}/tracks
    ↓
Return success/error
    ↓
Show toast notification
```

## Implementation Steps

### Phase 0: Refactoring & Test Coverage (Foundation)

**Goal**: Extract reusable Spotify logic from `CreateSpotify` and establish test coverage before adding new features.

1. **Extract Spotify Authentication & Validation Helper** (`m4d/Services/SpotifyAuthService.cs`)

   - [ ] Create `SpotifyAuthService` class with dependency injection
   - [ ] Extract `CanSpotify()` logic from `SongController`
   - [ ] Extract premium validation logic
   - [ ] Extract Spotify OAuth redirect logic
   - [ ] Add method to get login key for Spotify
   - [ ] Add XML documentation for all public methods

2. **Add Unit Tests for SpotifyAuthService** (`m4d.Tests/Services/SpotifyAuthServiceTests.cs`)

   - [ ] Test `CanSpotify()` returns true when user has Spotify OAuth
   - [ ] Test `CanSpotify()` returns false when no OAuth
   - [ ] Test premium validation with different role combinations
   - [ ] Test OAuth redirect URL construction
   - [ ] Mock `AdmAuthentication`, `UserManager`, and `HttpContext`

3. **Add Unit Tests for MusicServiceManager** (`m4dModels.Tests/MusicServiceManagerTests.cs`)

   - [ ] Test `CreatePlaylist()` with valid inputs
   - [ ] Test `AddToPlaylist()` with single track (POST method)
   - [ ] Test `SetPlaylistTracks()` with multiple tracks
   - [ ] Test error handling for Spotify API failures
   - [ ] Mock Spotify API responses
   - [ ] Test pagination handling for large track lists

4. **Refactor SongController.CreateSpotify** to use new services

   - [ ] Replace inline `CanSpotify()` with `SpotifyAuthService.CanSpotify()`
   - [ ] Replace inline premium check with `SpotifyAuthService.IsPremium()`
   - [ ] Replace OAuth redirect logic with `SpotifyAuthService.GetSpotifyOAuthRedirect()`
   - [ ] Verify existing CreateSpotify functionality still works
   - [ ] Run regression tests

5. **Document Refactored Code**
   - [ ] Add XML comments to `SpotifyAuthService`
   - [ ] Document MusicServiceManager Spotify methods
   - [ ] Update inline comments in `SongController.CreateSpotify()`

**Success Criteria for Phase 0**:

- [ ] All CreateSpotify functionality preserved (no regressions)
- [ ] SpotifyAuthService under test with >80% coverage
- [ ] MusicServiceManager Spotify methods under test with >80% coverage
- [ ] New service can be reused by Phase 1 API controller

### Phase 1: Backend Infrastructure

1. **Create data models** (`m4dModels`)

   - [ ] `AddToPlaylistRequest.cs`
   - [ ] `AddToPlaylistResult.cs`
   - [ ] Update `PlaylistMetadata` if needed

2. **Enhance MusicServiceManager** (`m4dModels/MusicServiceManager.cs`)

   - [ ] Add `GetUserPlaylists()` method
   - [ ] Add unit tests for `GetUserPlaylists()`
   - [ ] Test with Spotify API (integration test)
   - [ ] Handle pagination if user has >50 playlists

3. **Create API controller** (`m4d/APIControllers/SpotifyPlaylistController.cs`)

   - [ ] Inject `SpotifyAuthService` (from Phase 0)
   - [ ] Implement `GetUserPlaylists` endpoint using `SpotifyAuthService`
   - [ ] Implement `AddTrackToPlaylist` endpoint using `SpotifyAuthService`
   - [ ] Add proper error handling and logging
   - [ ] Add activity logging for tracking

4. **Testing**
   - [ ] Unit tests for controller methods (using mocked `SpotifyAuthService`)
   - [ ] Integration tests with mock Spotify API
   - [ ] Test all auth/permission scenarios

### Phase 2: Frontend Components

1. **Create PremiumRequiredModal** (`components/PremiumRequiredModal.vue`)

   - [ ] Implement modal with marketing content
   - [ ] Add props for feature name
   - [ ] Link to contribute page
   - [ ] Add unit tests

2. **Create AddToPlaylistButton** (`components/AddToPlaylistButton.vue`)

   - [ ] Implement button/dropdown UI
   - [ ] Add lazy playlist loading
   - [ ] Implement client-side caching
   - [ ] Add refresh functionality
   - [ ] Implement error handling for all states
   - [ ] Add toast notifications
   - [ ] Add loading states
   - [ ] Add unit tests

3. **Testing**
   - [ ] Unit tests for AddToPlaylistButton
   - [ ] Mock API responses
   - [ ] Test all error scenarios
   - [ ] Test authentication redirects

### Phase 3: Integration

1. **Integrate into PlayModal** (`components/PlayModal.vue`)

   - [ ] Import AddToPlaylistButton
   - [ ] Add conditional rendering based on Spotify track availability
   - [ ] Position between purchase links and audio player
   - [ ] Test in modal context

2. **Integrate into PurchaseSection** (`pages/song-details/components/PurchaseSection.vue`)

   - [ ] Import AddToPlaylistButton
   - [ ] Add to UI layout
   - [ ] Ensure consistent styling
   - [ ] Test on song details page

3. **Integration Testing**
   - [ ] Test full user flow from song page
   - [ ] Test full user flow from song list
   - [ ] Test with different user states (anon, logged in, premium, non-premium)
   - [ ] Test with Spotify-connected and non-connected accounts

### Phase 4: Documentation & Polish

1. **User Documentation**

   - [ ] Update help docs on blog
   - [ ] Add feature to premium benefits list
   - [ ] Screenshot examples

2. **Code Documentation**

   - [ ] Add XML comments to backend methods
   - [ ] Add JSDoc comments to Vue components
   - [ ] Update README if needed

3. **UI/UX Polish**
   - [ ] Ensure consistent button styling
   - [ ] Optimize loading states
   - [ ] Verify accessibility (keyboard navigation, screen readers)
   - [ ] Test on mobile devices

## Technical Considerations

### Reuse from CreateSpotify

The following patterns/methods can be reused from the existing `CreateSpotify` implementation:

1. **Authentication checks**:

   ```csharp
   // From SongController.cs line 926
   private async Task<bool> CanSpotify() =>
       await AdmAuthentication.HasAccess(
           Configuration, ServiceType.Spotify, User,
           await HttpContext.AuthenticateAsync());
   ```

2. **Premium validation**:

   ```csharp
   // From SongController.cs line 941
   info.IsPremium = User.IsInRole("premium") || User.IsInRole("trial");
   ```

3. **Spotify OAuth redirect**:

   ```csharp
   // From SongController.cs line 894-900
   var logins = await UserManager.GetLoginsAsync(applicationUser);
   if (logins.Any(l => l.LoginProvider == "Spotify"))
   {
       var returnUrl = Request.Path + Request.QueryString;
       var redirectUrl = $"/Identity/Account/Login?provider=Spotify&returnUrl={returnUrl}";
       return LocalRedirect(redirectUrl);
   }
   ```

4. **MusicServiceManager usage**:

   ```csharp
   // From SongController.cs line 986-989
   var service = MusicService.GetService(ServiceType.Spotify);
   await MusicServiceManager.AddToPlaylist(
       service, User, await GetLoginKey("Spotify"),
       playlistId, new[] { trackId }, HttpMethod.Post)
   ```

5. **Activity logging pattern**:
   ```csharp
   // From SongController.cs line 1024-1029
   if (await FeatureManager.IsEnabledAsync(FeatureFlags.ActivityLogging))
   {
       var user = await Database.FindUser(User.Identity?.Name);
       _ = Database.Context.ActivityLog.Add(
           new ActivityLog("SpotifyAddTrack", user, activityData));
       _ = await Database.SaveChanges();
   }
   ```

### Playlist Caching Strategy

**Client-side caching implementation**:

```typescript
// In AddToPlaylistButton.vue
const CACHE_KEY = "spotify_playlists";
const CACHE_DURATION = 1000 * 60 * 15; // 15 minutes

interface CachedPlaylists {
  playlists: PlaylistMetadata[];
  timestamp: number;
  userId: string;
}

const loadFromCache = (): PlaylistMetadata[] | null => {
  const cached = localStorage.getItem(CACHE_KEY);
  if (!cached) return null;

  const data: CachedPlaylists = JSON.parse(cached);
  const age = Date.now() - data.timestamp;

  // Invalidate if wrong user or too old
  if (data.userId !== menuContext.userId || age > CACHE_DURATION) {
    localStorage.removeItem(CACHE_KEY);
    return null;
  }

  return data.playlists;
};

const saveToCache = (playlists: PlaylistMetadata[]) => {
  const data: CachedPlaylists = {
    playlists,
    timestamp: Date.now(),
    userId: menuContext.userId!,
  };
  localStorage.setItem(CACHE_KEY, JSON.stringify(data));
};
```

**Benefits**:

- Reduces API calls (rate limiting consideration)
- Faster UX (no loading spinner on subsequent uses)
- Works offline-first for recently-loaded data

**Limitations**:

- May show stale data if user creates playlist elsewhere
- Solution: Manual refresh button in dropdown

### Future Enhancements (Out of Scope)

1. **Batch operations** from `SongTable`

   - Select multiple songs in search results
   - Add all selected to playlist in one operation
   - Requires UI for multi-select and batch API endpoint

2. **Create new playlist** option in dropdown

   - Add "Create New Playlist" option in dropdown
   - Inline form to create playlist and add song
   - Requires additional API endpoint

3. **Recently used playlists** quick-add

   - Track user's most-used playlists
   - Show shortcut buttons for top 3
   - Store in localStorage or user preferences

4. **Playlist search** for users with many playlists
   - Add search/filter in dropdown
   - Client-side filtering of cached playlists

## Testing Checklist

### Backend Tests

- [ ] Unit test: `GetUserPlaylists` returns playlists for authenticated user
- [ ] Unit test: `GetUserPlaylists` returns 401 for unauthenticated user
- [ ] Unit test: `AddTrackToPlaylist` succeeds for valid request
- [ ] Unit test: `AddTrackToPlaylist` returns 402 for non-premium user
- [ ] Unit test: `AddTrackToPlaylist` returns 403 for user without Spotify login
- [ ] Unit test: `AddTrackToPlaylist` returns 404 for song without Spotify track
- [ ] Integration test: Full flow with mock Spotify API

### Frontend Tests

- [ ] Unit test: `AddToPlaylistButton` renders correctly
- [ ] Unit test: Button shows loading state while fetching playlists
- [ ] Unit test: Dropdown shows playlists after load
- [ ] Unit test: Cache is used on second open
- [ ] Unit test: Refresh button clears cache and reloads
- [ ] Unit test: Success toast shown after add
- [ ] Unit test: Error modal shown for non-premium users
- [ ] Unit test: Redirect occurs for unauthenticated users
- [ ] Component test: Integration with PlayModal
- [ ] Component test: Integration with PurchaseSection

### Manual Testing Scenarios

- [ ] Anonymous user sees button, clicks → redirected to login
- [ ] Logged-in non-premium user clicks → sees premium modal
- [ ] Premium user without Spotify clicks → redirected to external logins
- [ ] Premium user with Spotify clicks → sees playlists
- [ ] Adding song to playlist shows success toast
- [ ] Refresh button reloads playlists
- [ ] Feature works in PlayModal
- [ ] Feature works on SongDetails page
- [ ] Song without Spotify track doesn't show button
- [ ] Mobile/responsive testing
- [ ] Accessibility testing (keyboard nav, screen reader)

## Success Criteria

1. ✅ Premium users can add songs to Spotify playlists from PlayModal
2. ✅ Premium users can add songs to Spotify playlists from song details page
3. ✅ Feature is visible to all users (for marketing)
4. ✅ Non-premium users see upgrade prompt
5. ✅ Unauthenticated users redirected to login
6. ✅ Users without Spotify login redirected to connect account
7. ✅ Playlists cached client-side after first load
8. ✅ Success/error feedback via toast notifications
9. ✅ Activity logged for analytics
10. ✅ Feature documented in help/blog

## Deployment Plan

### Pre-deployment

- [ ] Code review completed
- [ ] All tests passing
- [ ] Documentation updated
- [ ] Feature flag added (optional): `FeatureFlags.AddToSpotifyPlaylist`

### Deployment

1. Deploy backend changes first (controllers, models)
2. Deploy frontend changes (components, integrations)
3. Enable feature flag if used
4. Monitor logs for errors
5. Watch for user feedback

### Post-deployment

- [ ] Verify feature works in production
- [ ] Monitor activity logs for usage
- [ ] Check for errors in logs
- [ ] Gather user feedback
- [ ] Update blog/help with announcement

## Risk Mitigation

| Risk                        | Impact | Mitigation                                        |
| --------------------------- | ------ | ------------------------------------------------- |
| Spotify API rate limiting   | High   | Implement client-side caching, user rate limiting |
| OAuth token expiration      | Medium | Handle 401 responses, redirect to re-auth         |
| User has too many playlists | Low    | Implement pagination or search in dropdown        |
| Song missing Spotify ID     | Low    | Hide button when no Spotify track available       |
| Network errors during add   | Medium | Retry logic, clear error messages                 |

## References

### Existing Code to Study

- `m4d/Controllers/SongController.cs` (lines 886-1030) - CreateSpotify implementation
- `m4d/Views/Song/CreateSpotify.cshtml` - Premium/Spotify checks UI pattern
- `m4dModels/MusicServiceManager.cs` - Spotify API integration
- `m4d/ClientApp/src/components/PlayModal.vue` - Integration point
- `m4d/ClientApp/src/models/MenuContext.ts` - Auth state management

### External Documentation

- [Spotify Web API - Add Items to Playlist](https://developer.spotify.com/documentation/web-api/reference/add-tracks-to-playlist)
- [Spotify Web API - Get User's Playlists](https://developer.spotify.com/documentation/web-api/reference/get-list-users-playlists)
- [OAuth 2.0 Authorization](https://developer.spotify.com/documentation/web-api/concepts/authorization)

---

**Plan Version**: 1.0
**Last Updated**: 2025-11-28
