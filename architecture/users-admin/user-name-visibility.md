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

| Kind                   | Examples                                  | `IsPseudo` | Privacy applies?  |
| ---------------------- | ----------------------------------------- | ---------- | ----------------- |
| Real registered user   | `dwgray`, `forrest.csuy`                  | `false`    | yes               |
| Pseudo/proxy user      | m4d service accounts, Spotify proxy users | `true`     | no — always shown |
| Batch/algorithmic user | `batch`, `batch-s`, `tempo-bot`, etc.     | `true`     | no — always shown |

`ApplicationUser.IsPseudo => IsM4d || IsSpotify` (`m4dModels/ApplicationUser.cs`),
where `IsM4d` is true for any account whose email ends in `@music4dance.net`.
Batch/algorithmic accounts **are** ordinary rows in the `ApplicationUser`
(Identity) table — `batch`, `batch-a`, `batch-e`, `batch-i`, `batch-s`,
`batch-x` are seeded via `DanceMusicService.FindOrAddUser`, which defaults
the email to `{name}@music4dance.net` when none is given, making them
`IsPseudo` through the exact same mechanism as real m4d service accounts.
They are recognized client-side as well, via `UserQuery.systemUserNames` /
`algorithmicUserNames` (`m4d/ClientApp/src/models/UserQuery.ts`), purely for
friendly display names — that list is presentation-only and does not affect
visibility.

**Known issue:** despite this, most history entries — including `batch`
itself — currently render as `UNAVAILABLE` to anonymous visitors. This is
not an intentional anonymization; see
[Known issue: most history entries showing as UNAVAILABLE to anonymous visitors](#known-issue-most-history-entries-showing-as-unavailable-to-anonymous-visitors)
for the root-cause analysis.

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
recent PR — see [PR 168](https://github.com/music4dance/music4dance/pull/168) — was untangling):

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

## Known issue: most history entries showing as UNAVAILABLE to anonymous visitors

**Status: root cause identified; mitigated.** `InternalBuildDictionaries`
(`m4d/Utilities/UserMapper.cs:261`) now catches per-user instead of
per-build, so one malformed row can no longer truncate the rest of the
cache. The underlying bad row in production data still needs to be found and
fixed (see the diagnostic queries below) — the code change makes the
cache resilient to it, but doesn't explain why it exists.

A first hypothesis — that `tempo-bot`/`automerge` are never persisted to
the `ApplicationUser` table — was **ruled out**: a DB query confirmed
`tempo-bot`, `batch`, and `spotify` all exist with correct
`...@music4dance.net` / `...@spotify.com` emails (so `IsPseudo` and
`DecoratedName` compute correctly for them) and `Privacy = 255`. Despite
that, a real song history dump shows entries for `EthanH|P`, `JuliaS|P`,
`LincolnA|P`, `batch|P`, `batch-a|P`, `batch-i|P`, `batch-x|P` **all**
rendering as `UNAVAILABLE` to an anonymous viewer — including `batch`
itself, which is unambiguously present in the DB with the right shape.
Per-entry decoration logic (`AnonymizeAll`'s pipe-suffix / `DecoratedName`
check) is independently covered by passing unit tests with a clean,
manually constructed dictionary, so the bug isn't in that per-record logic
either.

The remaining explanation that fits **every** observed symptom — most
entries `UNAVAILABLE`, but "a couple of pseudo users" and some real users
correctly showing through (one as a GUID, i.e. "Anonymous") — is that
`UserMapper`'s static, process-wide cache
(`s_cachedUsers`/`s_cachedIds`, `m4d/Utilities/UserMapper.cs:9-13`) was
only **partially populated** by `InternalBuildDictionaries`
(`UserMapper.cs:261`) and has stayed that way ever since:

```csharp
private static async Task InternalBuildDictionaries(UserManager<ApplicationUser> userManager)
{
    foreach (var user in userManager.Users)
    {
        var roles = await userManager.GetRolesAsync(user);
        var logins = await userManager.GetLoginsAsync(user);
        var userInfo = new UserInfo { User = user, Roles = [...roles], Logins = [...] };
        s_cachedUsers.Add(user.UserName, userInfo);   // <- throws on null or duplicate key
        s_cachedIds.Add(user.Id, userInfo);
    }
    CacheTime = DateTime.Now;
}
```

If iteration hits a row that makes `Dictionary.Add` throw — a `UserName`
that's `null` (there's precedent for incomplete rows in this table:
`ApplicationUser.IsPlaceholder => StartDate == DateTime.MinValue`), or two
rows whose `UserName` is identical case-insensitively (the dictionaries use
`StringComparer.OrdinalIgnoreCase`) — the exception is **not** an EF
`SqlException`, so `BuildDictionaries`'s catch block
(`UserMapper.cs:248`, `catch (Microsoft.Data.SqlClient.SqlException ex)`)
does **not** catch it. It propagates out of that one unlucky request, but
everything added to `s_cachedUsers`/`s_cachedIds` _before_ the throw stays
in the static field. Because `BuildDictionaries` only retries when
`s_cachedUsers.Count == 0` (`UserMapper.cs:243`), and the partial dictionary
is no longer empty, **the cache never retries** — every subsequent request,
forever (until an app restart or a manual `UserMapper.Clear()`), uses that
permanently-incomplete snapshot. Any username — real or pseudo, however
correctly configured in the DB — that happened to be enumerated _after_ the
bad row simply isn't in the dictionary, and `AnonymizeAll` treats "not
found" as "could be a leak, hide it" → `*UNAVAILABLE*`. (`Anonymize`, used
for authenticated viewers, treats the identical "not found" case as "assume
already anonymized, pass through unchanged" — which is why this has never
been visible to a logged-in member.)

**Confirmed against a full export of production `AspNetUsers`
(`UserId`, `UserName`, `Email`, `Privacy` for all 2016 rows,
`local/users.tsv`):** zero duplicate `Id` (case-sensitive or not), zero
duplicate `UserName` (case-insensitively, matching the dictionaries'
`StringComparer.OrdinalIgnoreCase`), zero blank/null `UserName`, zero
malformed rows. **A `Dictionary.Add` collision on `UserName` or `Id` is
ruled out** — those are the two keys `s_cachedUsers`/`s_cachedIds` use, and
neither has a duplicate anywhere in the table.

There **are** three pairs of accounts sharing an `Email` (case-insensitive:
`wjdls0jq9ony6wwvkm4plzd0c@spotify.com`, `paulspiano@spotify.com`,
`1293412305@spotify.com` — each shared between two distinct `Id`/`UserName`
pairs, e.g. `AMBeaverton`/`ArthurMurrayBeaverton`). `Email` isn't a
dictionary key in `UserMapper`, so this can't be what crashes
`InternalBuildDictionaries`. It's more likely the explanation for the
**separate** recurring prod→dev import "duplicate user" error — if the dev
schema (or its restore step) enforces a unique index on `Email`/
`NormalizedEmail` that production's actual data no longer satisfies, that
would throw an unrelated, but similarly-named, "duplicate user" error
during import. Worth confirming this against the import tool's exact error
text, but it's likely a distinct issue from the `UNAVAILABLE` bug.

With `AspNetUsers` itself clean, the remaining likely culprit is
`GetRolesAsync`/`GetLoginsAsync` throwing for some specific user — neither
is visible in the `UserId`/`UserName`/`Email`/`Privacy` export, so this
needs either the application logs or a join against `AspNetUserRoles`/
`AspNetUserLogins`:

```sql
-- Orphaned role mappings: a UserRole row pointing at a Role that no
-- longer exists could make GetRolesAsync misbehave for that user.
SELECT ur.UserId, ur.RoleId
FROM AspNetUserRoles ur
LEFT JOIN AspNetRoles r ON ur.RoleId = r.Id
WHERE r.Id IS NULL;

-- Same idea for logins.
SELECT ul.UserId
FROM AspNetUserLogins ul
LEFT JOIN AspNetUsers u ON ul.UserId = u.Id
WHERE u.Id IS NULL;
```

The fastest path at this point is likely the deployed fix itself (see
below): once it's live, the new `Console.WriteLine` in
`InternalBuildDictionaries` will print the exact `Id`/`UserName` of
whichever row fails and why, which beats guessing at more queries. Also
worth checking application logs/App Insights around the last app
start/recycle for an unhandled exception out of `UserMapper`
— it would _not_ have been logged by the old code's `SqlException`-only
catch block, so look at ASP.NET's own unhandled-exception logging for the
period before this fix shipped.

**Fix applied:** `InternalBuildDictionaries` now wraps each user's
processing in its own `try`/`catch`, logging and skipping a row that fails
instead of letting the exception abort the whole loop. A future bad row
will be excluded from the cache (correctly falling back to
`*UNAVAILABLE*`/GUID handling for just that one user) rather than silently
truncating every user enumerated after it. Covered by
`UserMapperTests.GetUserNameDictionary_DuplicateUserName_SkipsBadRowAndKeepsBuilding`
(`m4d.Tests/Utilities/UserMapperTests.cs`).

This does **not** explain _why_ the bad row exists in production, and
clearing the cache (`/ApplicationUsers/ClearCache`) only resets the
in-memory snapshot — the next rebuild will simply skip the same bad row
again (now correctly) rather than including it. Finding and fixing that
row in the data is still open; the diagnostic queries above are the
starting point.

---

## Open questions — resolved

- **Authenticated-but-not-the-owner access to a private user's song
  lists.** Confirmed intentional: a user who sets `Privacy != 0` has opted
  in to other logged-in members seeing their lists (e.g. a dance teacher
  publishing their liked Waltz songs). Privacy only withholds names/lists
  from anonymous/logged-out visitors, not from other members.
- **Granularity of `Privacy`.** Confirmed intentional for now — the
  `byte` field may become a real bit-field for finer-grained privacy
  levels later, but today it's deliberately treated as a boolean
  (`== 0` vs `!= 0`) everywhere.
- **`MustRegister` copy.** Resolved — the profile page's `MustRegister`
  title now reuses the exact same copy as `UserList.vue`'s gate. Read as
  "register and log in, and you'll get access," not as a promise of
  immediate access, the two are now consistent with each other and with
  actual behavior.
