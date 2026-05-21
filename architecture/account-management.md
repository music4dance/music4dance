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
