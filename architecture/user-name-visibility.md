# User Name Visibility

## Overview

The site shows user attribution in two places: the **profile page**
(`/users/info/{id}`) and **song edit history** (favorites/edits/blocked
lists, the edit log shown on song detail pages and search results). Both
places must decide, per viewer and per user, whether to show a real
username, a pseudonymous label, an opaque GUID ("anonymous"), or
`*UNAVAILABLE*`/`UNAVAILABLE`.

There are three kinds of "user" in this system, and the visibility rule is
different for each:

| Kind                   | Examples                                  | `IsPseudo`                     | Privacy applies?  |
| ---------------------- | ----------------------------------------- | ------------------------------ | ----------------- |
| Real registered user   | `dwgray`, `forrest.csuy`                  | `false`                        | yes               |
| Pseudo/proxy user      | m4d service accounts, Spotify proxy users | `true`                         | no — always shown |
| Batch/algorithmic user | `batch`, `batch-s`, `tempo-bot`, etc.     | n/a (not an `ApplicationUser`) | no — always shown |

`ApplicationUser.IsPseudo => IsM4d || IsSpotify` (`m4dModels/ApplicationUser.cs`).
Batch/algorithmic names are not `ApplicationUser` rows at all — they're
literal strings recognized client-side by `UserQuery.systemUserNames` /
`algorithmicUserNames` (`m4d/ClientApp/src/models/UserQuery.ts`) and
server-side by the `|P` decoration check in `UserMapper.AnonymizeAll`.

There are two independent axes that determine what a _real_ user's name
renders as:

1. **The user's own privacy setting** — `ApplicationUser.Privacy` (`byte`,
   0–255). `Privacy == 0` means "maximum privacy" (`Anonymous => Privacy == 0`
   in `m4dModels/ApplicationUser.cs:118`). Any non-zero value is currently
   treated as fully public — there's no graduated privacy level in use today,
   it's an on/off switch.
2. **Whether the current viewer is authenticated** — `User.Identity.IsAuthenticated`.
   Unauthenticated visitors never see a real username for a real user,
   regardless of that user's privacy setting.

```
                         viewer authenticated?
                    ┌───────────────┬───────────────┐
                    │      yes      │       no       │
   ┌────────────────┼───────────────┼───────────────┤
   │ Privacy == 0    │   GUID        │     GUID       │
   │ (private)       │ ("Anonymous") │  ("Anonymous")  │
   ├────────────────┼───────────────┼───────────────┤
   │ Privacy != 0    │  real name    │     GUID       │
   │ (public)        │               │  ("Anonymous")  │
   └────────────────┴───────────────┴───────────────┘
```

Pseudo and batch/algorithmic users are exempt from this table entirely —
they always render as themselves (or their friendly display name) to every
viewer, authenticated or not, because they aren't personal data.

`*UNAVAILABLE*` (server) / `UNAVAILABLE` (client display) is a third,
orthogonal state: it means the lookup itself failed (user deleted, or the
user database is unreachable and there's no cache), not a privacy decision.
See [Failure mode: `*UNAVAILABLE*`](#failure-mode-unavailable) below.

---

## Two call paths, two different jobs

`UserMapper` (`m4d/Utilities/UserMapper.cs`) is the single place that maps
between real usernames and GUIDs. It exposes two conceptually distinct
operations that must not be conflated (this conflation is exactly what the
recent PR — see `local/pr-anonymous-user-fixes.md` — was untangling):

- **`Deanonymize`** — GUID → real username, **unconditionally**, with no
  privacy or auth check. This is for _backend_ operations only: searching
  the Azure Search index (which is keyed by real username, not GUID) and
  writing history entries. A private user's profile page still has to be
  able to look up "this user's favorites" by real username even though the
  GUID is the only thing ever shown on screen.
- **`Anonymize` / `AnonymizeAll`** — real username → GUID, conditionally,
  for _display_. This is the privacy decision described in the table above.

Before the recent fix, `Deanonymize` itself was applying the privacy check
(refusing to resolve a GUID back to a username for `Privacy == 0` users).
That broke the song-list links on a private user's own profile page,
because the search call received a GUID instead of the username the index
is keyed by. The fix: `Deanonymize` always resolves; `Anonymize`/`AnonymizeAll`
are the only places privacy is enforced, and only on the way _out_ to the
client.

### `Anonymize` vs `AnonymizeAll`

Both take a real username and a `UserInfo` dictionary and decide what to
hand to the client. They differ only in whether they honor the user's own
privacy setting:

- **`Anonymize(userName, dictionary)`** — used when the viewer is
  authenticated. Honors `Privacy`: returns the GUID if `Privacy == 0`,
  otherwise the real username.
- **`AnonymizeAll(userName, dictionary)`** — used when the viewer is
  **not** authenticated. Ignores `Privacy` entirely and returns the GUID
  for every real user — public or private. Pseudo/batch users (detected via
  the `|P`-decorated name, see `ModifiedRecord.IsPseudo` /
  `ApplicationUser.DecoratedName`) pass through unchanged even here.

`AnonymizeHistory` (both the cached-dictionary overload and the
`UserManager`-based async overload) takes an `isAuthenticated` flag and
dispatches to one or the other. Every caller that touches song edit history
must pass the real `User.Identity.IsAuthenticated` value:

- `APIControllers/SongController.cs` — batch song GET, single-song GET
- `Controllers/SongController.cs` — `AnonymizeSongs()` (used by song list
  pages) and `GetSongDetails()`
- `Controllers/CustomSearchController.cs` — holiday/custom search page
- `Controllers/SongController.cs:DownloadJson` (admin-only, `[Authorize(Roles = "dbAdmin")]`)
  calls the 4-argument overload with no explicit flag — defaults to
  `isAuthenticated = true`, which is correct because the endpoint is gated
  on the `dbAdmin` role and an admin should always see real names.

---

## Profile page (`UsersController.Info`)

`UsersController.Info(string id)` (`m4d/Controllers/UsersController.cs:25`)
computes `profileUserName` once, up front:

```csharp
var profileUserName = (user.Privacy == 0 || !isAuthenticated) && !user.IsPseudo
    ? user.Id
    : user.UserName;
```

This mirrors the table above directly: GUID if private OR viewer
unauthenticated, unless the target is a pseudo user (always real name).

The value placed in `UserProfile.UserName` is what the client treats as the
identity of the profile being viewed — `ProfileModel.isAnonymous` (TS) is
derived purely from whether that string looks like a GUID
(`UserQuery.isAnonymous`: 36 chars, contains `-`). There is no separate
server-sent boolean for "is this anonymous" — the client infers it from the
shape of the string it was given. This is also why the controller must
**always** put the GUID in `UserName` for a private user regardless of how
the URL was formed (by username or by GUID) — if the real username ever
reaches the client in that field, `isAnonymous` silently becomes `false`
and the real name leaks into the page title, `<title>`, and any link built
from it.

### Non-existent users vs. private users

If `id` doesn't resolve to any user at all, the controller returns a fixed
placeholder profile (`UserName = "anonymous"`) instead of 404 or echoing
`id` back. This is deliberate: a 404 (or an echo of the requested id) would
let an attacker distinguish "no such user" from "user exists but is
private," i.e. username enumeration. Both cases must look identical from
the outside.

### Profile page rendering (`UserProfile.vue`)

The template is an ordered `v-else-if` chain — order matters because
several conditions can be simultaneously true in principle (e.g. a private
pseudo user):

1. `model.spotifyId` set → Spotify proxy blurb (always shown, no auth gate)
2. `model.isPseudo` → generic pseudo-user blurb (always shown, no auth gate
   — pseudo users aren't people, so there's nothing to protect)
3. `!menuContext.isAuthenticated` → `<MustRegister>` prompt (real users only
   reach this branch; pseudo/spotify users were already handled above)
4. authenticated, real user:
   - `model.isAnonymous` (i.e. `UserName` is a GUID, meaning `Privacy == 0`)
     → "this user has chosen to remain anonymous" message
   - otherwise → the (currently placeholder/under-development) profile text,
     with the "manage your privacy preference" paragraph gated further on
     `isCurrentUser`

`isCurrentUser` itself has to branch on `model.isAnonymous`, because once a
private user's identity has been reduced to a GUID, the only way to
recognize "this is actually me looking at my own profile" is to compare
that GUID against the viewer's own id (`menuContext.userId`), not against a
username comparison that will never match a GUID.

### Song lists (`UserList.vue`)

Independent of the profile blurb above, the favorites/edits/blocked song
lists on the same page are gated solely on `menuContext.isAuthenticated`:
unauthenticated visitors get a `<MustRegister>` prompt instead of list
content, but the list heading still renders above the prompt so the page
isn't a blank wall. There is no separate privacy check here for the
_target_ user — if you can't see anyone's song lists while logged out, the
target's own privacy setting is moot. (Whether a logged-in visitor should
be able to see a private _other_ user's song lists, given that the search
backend is necessarily called with their real username via
`Deanonymize`, is a question this document doesn't resolve — see
[Open questions](#open-questions).)

### Performance side-effect

Because unauthenticated visitors never see counts, `Info` skips the three
`SongIndex.UserSongCount` Azure Search calls entirely when
`!isAuthenticated`, and skips writing to the server-side profile cache
(`s_userCache`) too — caching a profile keyed by `id` for an anonymous
visitor would otherwise create a path for cached counts to leak across
session boundaries. Authenticated requests still use the cache and run the
three count queries in parallel via `Task.WhenAll`.

---

## Song edit history

History entries are a different surface from the profile page: they show
**every** user who has ever touched a song (via edits, votes, etc.), not
just the one profile being viewed. The relevant privacy question is "for
this viewer, should this entry's author be a real name or a GUID?" — answered
independently for every entry, using whichever of `Anonymize`/`AnonymizeAll`
matches the viewer's auth state (see above).

Two important asymmetries versus the profile page:

- The profile page's rule depends on `Privacy == 0` _and_ auth state, for
  the one user being viewed. History entries, when the viewer is
  unauthenticated, hide **every** real user's name (`AnonymizeAll` ignores
  `Privacy` and always returns the GUID) — there's no "public" carve-out in
  the unauthenticated case for history. When the viewer is authenticated,
  history falls back to the same `Privacy`-based rule as the profile page
  (`Anonymize`).
- Pseudo/batch users are recognized differently in each path. The profile
  page uses `ApplicationUser.IsPseudo` directly (it has the actual user
  row). History entries are decorated strings (`username|P`, see
  `ModifiedRecord.cs`), so `AnonymizeAll` has to split off the `|P` suffix,
  look up the base name, and confirm `DecoratedName` matches before letting
  it through unchanged — a plain dictionary miss on the full decorated
  string would otherwise fall through to the "not found" branch and get
  replaced with `*UNAVAILABLE*` or left untouched incorrectly.

### Round-tripping for edits (`Deanonymize` / client `SongHistory.Deanonymize`)

When a user opens the edit UI for a song, the client needs to know whether
a given history entry's `User` field was _them_, even though the server
sent a GUID for privacy. `SongTable.vue` calls
`history.Deanonymize(userName, userId)` (`m4d/ClientApp/src/models/SongHistory.ts`)
client-side to substitute the **viewer's own** username back in wherever
the GUID matches the viewer's own id — this is a pure display
substitution scoped to "is this me," not a privacy bypass, and it never
runs for other users' GUIDs.

Server-side, `UserMapper.DeanonymizeHistory` does the inverse for the
opposite direction: when a client submits an edit/vote referencing the
current user by GUID (because that's what they were shown), the server
resolves it back to the real username before writing to the search index,
since the index is keyed by username, not GUID.

---

## Failure mode: `*UNAVAILABLE*`

`*UNAVAILABLE*` (server-side constant in `UserMapper.cs`) / `UNAVAILABLE`
(client-side display string from `UserQuery.displayName`, detected via
`isUnavailable` when `userName === "*unavailable*"`) is returned when a
lookup cannot be resolved — it overlaps with, but is distinct from, "is
private":

- `Anonymize`: if the user dictionary is empty (DB unavailable, no cache) →
  `*UNAVAILABLE*`. If the dictionary has data but this particular name
  isn't in it, the value is assumed to already be a previously-anonymized
  GUID and is passed through unchanged.
- `AnonymizeAll`: same empty-dictionary case → `*UNAVAILABLE*`. If the
  dictionary has data but the name isn't found, it's passed through
  unchanged only if it already looks like a GUID (36 chars, contains `-`);
  otherwise it's replaced with `*UNAVAILABLE*` rather than leaking an
  unrecognized raw string to an unauthenticated viewer.

In short: an empty user dictionary always means `*UNAVAILABLE*`; a
populated dictionary with a miss is treated as "already anonymized" by
`Anonymize` but as "leak risk, hide it" by `AnonymizeAll` — `AnonymizeAll`
is intentionally the more conservative of the two since its whole purpose
is protecting users from an audience that hasn't even logged in.

---

## Quick reference: what does this viewer see?

| Target user kind        | Viewer authenticated | Target `Privacy` | Profile page shows  | History entry shows                  |
| ----------------------- | -------------------- | ---------------- | ------------------- | ------------------------------------ |
| Real user               | yes                  | `0` (private)    | GUID ("Anonymous")  | GUID ("Anonymous")                   |
| Real user               | yes                  | `!= 0` (public)  | real username       | real username                        |
| Real user               | no                   | any              | GUID ("Anonymous")  | GUID ("Anonymous")                   |
| Pseudo/proxy user       | yes or no            | n/a              | real (pseudo) name  | real (decorated) name                |
| Batch/algorithmic user  | yes or no            | n/a              | n/a (not a profile) | friendly system name (client-mapped) |
| Lookup failed / DB down | yes or no            | n/a              | n/a                 | `*UNAVAILABLE*` / `UNAVAILABLE`      |

---

## Open questions

These are not bugs, but are worth resolving deliberately before making
further changes (this doc was requested specifically to surface them):

- **Authenticated-but-not-the-owner access to a private user's song
  lists.** The profile page's song-list gate only checks
  `menuContext.isAuthenticated`, not whether the _viewer_ is the _target_
  user or whether the target is private. Any logged-in member can view
  another private member's favorites/edits/blocked counts and click
  through to the underlying song list. Is that intended (privacy only
  protects against anonymous/logged-out visitors, not other members), or
  should a private user's lists be restricted to themselves once
  authentication is required anyway?
- **Granularity of `Privacy`.** The field is a `byte` (0–255) but every
  code path treats it as a boolean (`== 0` vs `!= 0`). If finer-grained
  privacy levels are ever desired, `Anonymize`/`AnonymizeAll`/the profile
  controller are the three places that would need to change together.
- **`MustRegister` copy on the profile page** currently tells an
  unauthenticated visitor they "will be able to see their song lists" even
  though `UserList.vue` shows its own `MustRegister` gate for exactly that
  content — i.e., the promise in the profile blurb is never actually kept
  for logged-out visitors. Worth reconciling the copy with Feature 6's
  behavior.
