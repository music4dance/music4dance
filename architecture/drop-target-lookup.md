# Inline Service-ID Lookup (`useDropTarget.ts`)

## Overview

`useDropTarget` (`m4d/ClientApp/src/composables/useDropTarget.ts`) lets a user paste a Spotify
track ID/URL or an Apple Music track ID/URL into an ordinary text input elsewhere on the site —
not a dedicated "add song" form — and get routed straight to the matching catalog song (or an
offer to add it) instead of having the pasted text run as a literal search term. It is the
"opportunistic" counterpart to the deliberate by-ID lookup in Augment
([[add-augment-song]] § "By ID (`AugmentLookup.vue`)"); both converge on the same
`GET /api/servicetrack/{cid}{id}` endpoint documented in [[service-track-lookup]].

This is a small composable with no local state — `matcher: ServiceMatcher` is a module-level
singleton, so `useDropTarget()` can be called from any component without re-instantiating it.

## Call Sites

| Component | Input | Trigger |
| --- | --- | --- |
| `src/components/SuggestionEntry.vue` | Main site search-as-you-type box | `@input="checkServiceAndWarn($event.target.value, danceId)"` |
| `src/pages/advanced-search/components/KeywordEditor.vue` | Advanced-search keyword field | `updateModel` calls `checkServiceAndWarn(value)` before applying the keyword to the query |
| `src/pages/spotify-explorer/App.vue` | Admin tool's "look up by string" box | `handleSearch`'s `ServiceObjectKind.Track` case calls `checkServiceAndWarn(serviceString.value)` |

All three call only `checkServiceAndWarn` — the `warn: false` branch of `checkServiceAndAdd`
(silently returning without offering to add) is exported but has no current caller.

## Decision Tree

```
checkServiceAndAdd(input, warn, danceId)
  ├─ matcher.match(input) → no match?  → return (treat input as an ordinary search string)
  └─ match found → checkService(input)
        ├─ matcher.parsePlaylist(input) matches (Spotify playlist URL)?
        │     → navigate to /song/playlist?id={playlistId}; return true
        ├─ matcher.parseId(input, service) extracts an id →
        │     matcher.findSong(input, localOnly=true)
        │       → GET /api/servicetrack/{cid}{id}?localOnly=true
        │     ├─ song found → navigate to /song/details/{song.songHistory.id}; return true
        │     └─ not found (404, or any request error — swallowed) → return false
        └─ checkService returned false, and warn === true →
              show confirm modal: "It looks like you may have tried to search by
              {service.name} id for a song not in the music4dance catalog.
              Would you like to add the song?"
              ├─ user confirms → navigate to
              │     /song/augment?id={id}&dance={danceId}
              │     (Augment's by-ID tab takes over from here — see [[add-augment-song]]
              │     and [[service-track-lookup]]; this is where the side-effecting
              │     create/merge actually happens)
              └─ user declines → no navigation, input is left as typed
```

`checkService` is also exported on its own and reused by `checkServiceAndAdd`; it is the
"does this already exist, non-mutating" half of the flow — it always calls the lookup endpoint
with `localOnly=true`, so pasting an ID that isn't in the catalog yet never creates or modifies
anything by itself. Only following through on the confirm modal (which lands on `/song/augment`)
can trigger the side-effecting create path.

## Recognition (`ServiceMatcher.ts`)

`matcher.match(input)` walks a fixed list of `{id, name, rgx}` entries
(`m4d/ClientApp/src/helpers/ServiceMatcher.ts`) and returns the first whose regex matches:

| Service | `id` | Patterns |
| --- | --- | --- |
| Apple Music | `i` | bare 7–10 digit number; `https://(music\|itunes).apple.com/…/{7-10 digits}?i={7-10 digits}` |
| Spotify | `s` | bare 22-character alphanumeric string; `https://open.spotify.com/track/{22 chars}` |

There is no ISRC pattern registered — pasting a raw ISRC (e.g. `USRC17607839`) into any of the
three input boxes above does not match either regex, so it is always treated as a literal search
string today, regardless of any server-side ISRC-based fallback logic (see
[[isrc-spotify-fallback-plan]] in `local/` for the fallback proposal). Extending `services` here
with an ISRC pattern would be a separate, purely additive client change if that UX is ever wanted.

`ServiceMatcher` also recognizes Spotify playlist URLs (`parsePlaylist`) and Spotify user profile
URLs (`parseUser`, used by `findSpotifyUser` — not part of the `useDropTarget` flow, called
directly from `spotify-explorer/App.vue` for the "User" object kind).

## Error Handling

`checkService` wraps the entire lookup in a bare `try { } catch { }` that swallows *any* error
(network failure, non-2xx response, JSON parse failure) and falls through to `return false`. This
is intentional: a failed lookup should degrade to "treat the pasted text as a normal search term,"
not surface an error to the user typing in a search box.

## Related Code

| File | Purpose |
| --- | --- |
| `m4d/ClientApp/src/composables/useDropTarget.ts` | `checkServiceAndAdd`/`checkServiceAndWarn`/`checkService` |
| `m4d/ClientApp/src/helpers/ServiceMatcher.ts` | Regex-based id/URL/playlist/user recognition, `findSong`/`findSpotifyPlaylist`/`findSpotifyUser` |
| `m4d/ClientApp/src/components/SuggestionEntry.vue` | Main search box call site |
| `m4d/ClientApp/src/pages/advanced-search/components/KeywordEditor.vue` | Advanced search keyword field call site |
| `m4d/ClientApp/src/pages/spotify-explorer/App.vue` | Admin Spotify-explorer tool call site |
| `m4d/APIControllers/ServiceTrackController.cs` | Backend endpoint hit by `matcher.findSong` |
