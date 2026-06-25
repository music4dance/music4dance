# Account Management

## Overview

music4dance.net uses ASP.NET Core Identity for authentication and user management.
Registration requires email confirmation. Each user has a username, email, and privacy setting.

---

## Username Policy

### Allowed Characters

- The built-in ASP.NET Identity character restriction is **disabled** (`AllowedUserNameCharacters = ""`)
- A custom `UsernameValidator` is registered instead
- **Allowed**: all characters **except** `:`, `;`, `,`, `|`, `@`

```
Blocked set: : ; , | @
```

These characters are blocked because they are structural delimiters in the application's internal
formats (SongFilter, UserQuery, OData filters, and tag strings).

### Why `|` Is Blocked

The `UserQuery` format is `(+|-)username|modifier` where `|` separates the username from the
modifier character (`l`, `h`, `a`, `d`, `x`). If `|` were allowed in a username, it would break
`UserQuery` parsing:

- `UserQuery.UserName` extracts via `Query[1..Query.IndexOf('|')]`
- A username containing `|` would cause the wrong substring to be extracted

### Period (`.`) in Usernames

The period character **is allowed** in usernames (e.g., `forrest.csuy`). This is intentional.

**URL handling:**

- In URL **query strings** (e.g., `?user=forrest.csuy`): periods are unreserved characters per
  RFC 3986 and require no encoding. These work correctly in all endpoints.
- In URL **path segments** (e.g., `/users/info/forrest.csuy`): periods are also valid but some
  older IIS configurations may treat segments ending in an extension-like suffix (`.csuy`) as
  static file requests. On Azure App Service with ANCM v2 (in-process), all requests pass through
  the ASP.NET Core pipeline regardless of extension, so this is not an issue in production.
- In the **SongFilter serialization format**: the delimiter is `-` and periods are transparent —
  `forrest.csuy` in the User field parses and round-trips correctly.
- In **Azure Search OData filters**: periods are valid in string literals:
  `Users/any(t: t eq 'forrest.csuy')` is a well-formed filter.

**URL generation best practice:** When embedding a username in a URL path segment, always use
`encodeURIComponent(username)` to handle any future allowed characters that would require encoding.
The `UserLink.vue` component does this for path-based user URLs.

### Other Implications of Allowing Periods

- Usernames like `forrest.csuy` look like domain names or file names — this can confuse some
  tooling but is not a functional problem
- Azure App Service has no known issues with periods in ASP.NET Core route parameters

---

## Password Policy

Password requirements use ASP.NET Core Identity defaults (not overridden in `Program.cs`):

| Requirement              | Value |
| ------------------------ | ----- |
| Minimum length           | 6     |
| Require digit            | Yes   |
| Require lowercase        | Yes   |
| Require uppercase        | Yes   |
| Require non-alphanumeric | Yes   |
| Required unique chars    | 1     |

Password hashing uses `PasswordHasherCompatibilityMode.IdentityV2`.

---

## Privacy Setting

Each user has a `Privacy` byte field:

- `0` — maximum privacy; username is anonymized to the user's GUID in public-facing searches
- `255` — public; username is visible in search results and history

**Default**: new users are created with `Privacy = 255` (public) in `Register.cshtml.cs`.

**Effect on song searches:** When someone other than the user searches for songs by that user
(e.g., via `/Song/FilterUser?user=someuser`), `SongSearch.Search()` calls `AnonymizeFilter`
followed by `DeanonymizeFilter`:

- If `Privacy = 0`: the username is replaced with the user's GUID for the Azure Search OData
  filter. Since songs are indexed with the lowercase username (not a GUID), this returns zero
  results. This is by design — a private user's songs are hidden from others.
- If `Privacy = 255`: the username is passed through unchanged; songs are found normally.

**Admin bypass**: Users with the `dbAdmin` role are exempt from the privacy filter. When a
`dbAdmin` performs a user song search, `SongSearch` skips the `AnonymizeFilter` step entirely,
so the username is used as-is in the Azure Search query and all songs are returned regardless
of the target user's privacy setting. This is implemented via the `isAdmin` parameter on
`SongSearch`, populated from `User.IsInRole(DanceMusicCoreService.DbaRole)` in `DoAzureSearch`.

---

## Account Setup & Lockout

| Setting                     | Value      |
| --------------------------- | ---------- |
| Requires email confirmation | Yes        |
| Requires unique email       | Yes        |
| Lockout on failed login     | 3 attempts |
| Lockout duration            | 15 minutes |
| Lockout for new users       | Yes        |

---

## UserQuery Format and Usernames

The `UserQuery` class normalizes a username into the internal format `(+|-)username|modifier`:

- `+` prefix = include songs matching this user
- `-` prefix = exclude songs matching this user
- Modifier: `l`=liked, `h`=don't like, `a`=any activity, `d`=upvoted, `x`=downvoted, none=any

For `FilterUser?user=forrest.csuy`:

1. `Filter.User = "forrest.csuy"` (raw from query string)
2. `UserQuery.UserName` = `"forrest.csuy"` (extracted via `Query[1..Query.IndexOf('|')]`)
3. Azure OData filter = `(Users/any(t: t eq 'forrest.csuy') or Users/any(t: t eq 'forrest.csuy|l'))`

The period does not interfere with any of these steps.

---

## Diagnosing "User Has No Songs" Issues

If `FilterUser?user=someuser` returns no results:

1. **Check privacy setting**: if `Privacy = 0`, songs are hidden from non-admins. Admins
   (`dbAdmin` role) bypass this and will always see results.
2. **Check indexing**: Azure Search indexing may lag. Songs added recently may not appear until
   the index is updated.
3. **Verify the username**: use the admin users list to confirm the exact username stored in the
   database (case-sensitive lookup is done case-insensitively, but the index stores lowercase).
4. **Check for anonymized songs**: songs added by the user before their account was linked may
   be attributed to a GUID or a different username.

---

## External Login

External login providers (e.g., Google, Microsoft) require the user to choose a username when
first logging in via `ExternalLogin.cshtml`. The same username validator applies.

---

## User Deletion and Vote Preservation

When an admin deletes a user via `ApplicationUsersController.DeleteConfirmed`, their song
contributions (votes, tags, edits) are **preserved but permanently anonymized** rather than
deleted. This is done by calling `Database.ChangeUserName` before removing the account:

```csharp
await Database.ChangeUserName(applicationUser.DecoratedName, applicationUser.Id);
```

### How It Works

`ChangeUserName` finds all songs in Azure Search where the user's `DecoratedName` appears in the
`UserField` or `UserProxy` properties, then rewrites those properties with the new name. By
passing the user's GUID as the new name, all contributions are attributed to an anonymous identity
that happens to look like a GUID.

### Why a GUID Means "Anonymous"

GUIDs (36 characters containing `-`) are detected as anonymous identities throughout the stack:

- **`UserQuery.IsAnonymous`** (TypeScript): `userName.length === 36 && userName.includes('-')` →
  displays as "Anonymous" in song history and filter descriptions
- **`UserQuery.IsAnonymous`** (C#): same check → display and filter logic treats them as anonymous
- **`UserMapper.AnonymizeAll`**: explicitly passes GUIDs through unchanged (already anonymized)
- **`UserMapper.Deanonymize`**: GUID not found in dictionary → returns GUID unchanged (no-op)

### Why the GUID Is Permanently Anonymous

After the user is deleted the GUID is no longer in the user database, so:

- `AnonymizeFilter` and `DeanonymizeFilter` cannot resolve it → passes through as-is
- Admin operations that deanonymize history find no match → GUID stays as-is
- There is no way to reverse the anonymization after deletion — this is intentional

### What Is Also Deleted

- The user's saved searches (`Context.Searches`) are hard-deleted
- The `ApplicationUser` row is hard-deleted
- `UserMapper.Clear()` is called to invalidate the in-memory user cache

---

## User Merge

Pseudo (service) users are sometimes created more than once for the same real-world identity — most
commonly when a Spotify playlist owner renames their Spotify screen name. `ServicePlaylistController.Post`
calls `Database.FindUser(serviceList.OwnerName) ?? await Database.AddPseudoUser(name, email)`, so a rename
that doesn't match the existing username creates a second pseudo `ApplicationUser` with the same
`@spotify.com` email as the original — producing duplicate-email pairs.

`DanceMusicService.MergeUsers(keepId, mergeId)` (`m4dModels/DanceMusicService.cs`) merges one pseudo user
into another:

1. Both users must already exist and have `IsPseudo == true` (registered/real users are rejected) — this is
   a guard rail, not a general-purpose account-merge tool.
2. **Playlists**: every `PlayList` row where `User == mergeUser.UserName` is repointed to
   `keepUser.UserName` (see [[playlist-management]] — `PlayList.User` stores the plain username, not the Id).
3. **Saved searches and activity log**: rows referencing `mergeUser.Id` are reassigned to `keepUser.Id`.
   This must happen before the user row is removed — `Search.ApplicationUserId` has a `Restrict` delete
   behavior (and `ActivityLog`'s FK would otherwise block deletion too).
4. **Song contributions**: `ChangeUserName(mergeUser.DecoratedName, keepUser.DecoratedName)` rewrites every
   `UserField`/`UserProxy` `SongProperty` in the search index from the merged-away user's decorated name to
   the kept user's — the same mechanism used for the anonymize-on-delete flow above, but pointed at another
   live username instead of a GUID.
5. The merged-away `ApplicationUser` row is removed via `Context.Users.Remove` (roles/logins cascade-delete
   automatically through the Identity FK configuration).
6. The in-memory `FindUser` cache entries for both users are cleared so subsequent lookups re-read from the
   database.

### Admin UI

`ApplicationUsersController.Merge` / `MergeConfirm` / `MergeConfirmed` expose this as a two-step admin flow
(`/ApplicationUsers/Merge`): enter the username to keep and the username to merge away, review both
accounts' UserName/Id/Email on a confirmation page, then submit to perform the merge. The admin users table
has a per-row "Merge" link (pseudo users only) that pre-fills the "merge away" field. As with delete,
`UserMapper.Clear()` is called after a successful merge.
