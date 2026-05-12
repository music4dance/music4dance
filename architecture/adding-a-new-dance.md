# Adding a New Dance Type

## Overview

This document describes every step required to add a new dance type to music4dance. The
process is primarily data-driven: the vast majority of the system auto-discovers dances from two
JSON files. Hardcoded changes are only needed when introducing a **new major navigation group**
(like the planned "Pattern" group described below).

Related documents:

- **[song-internal-format.md](song-internal-format.md)** — song/tag serialization format
- **[SongUploadFormat.md](SongUploadFormat.md)** — bulk TSV upload for songs

---

## Dance Categories

Each dance falls into exactly one of these categories, which determines how it is configured:

| Category               | Examples                                  | meter            | `competitionGroup` | `style`                                               |
| ---------------------- | ----------------------------------------- | ---------------- | ------------------ | ----------------------------------------------------- |
| Ballroom competition   | Waltz, Cha Cha, Tango                     | exact (e.g. 3/4) | `"Ballroom"`       | `"International Standard"`, `"American Rhythm"`, etc. |
| Country competition    | Country Waltz, CHA                        | exact            | `"Country"`        | `"Country"`                                           |
| Social partner dance   | Salsa, Bachata, Night Club 2-Step         | exact            | omit               | `"Social"`                                            |
| Performance / no-meter | Jazz, Broadway, Bollywood                 | `1/1`            | omit               | `"Performance"`                                       |
| Pattern / no-meter     | _(new)_ Line Dance, Choreographed Partner | `1/1`            | omit               | `"Social"`                                            |

> **Pattern dance note:** The "Pattern" dance type (for line dances and choreographed partner
> dances) uses `meter: 1/1` and no `competitionGroup`, like Performance dances, but uses
> `style: "Social"` because these are primarily social dances. It is placed under the existing
> **"Other"** group (`MSC`) in `dancegroups.json`.

---

## Step-by-Step Guide

### Step 1 — Choose a 3-character Dance ID

Dance IDs are exactly **3 uppercase ASCII letters** and must be globally unique.

- Check all existing IDs in `m4d/ClientApp/src/assets/content/dances.json`.
- IDs are used as permanent database keys; do **not** reuse or rename them.

**Planned example:** `PTN` for a generic "Pattern" dance type.

---

### Step 2 — Add the Dance to `dances.json`

**File:** `m4d/ClientApp/src/assets/content/dances.json`

Add a new JSON object to the top-level array. The required structure depends on category:

#### Ballroom / Country Competition Dance

```json
{
  "id": "XYZ",
  "name": "My Dance",
  "meter": { "numerator": 4, "denominator": 4 },
  "blogTag": "my-dance",
  "synonyms": ["Alternate Name"],
  "instances": [
    {
      "style": "International Standard",
      "organizations": ["DanceSport", "NDCA"],
      "tempoRange": { "min": 100.0, "max": 120.0 },
      "competitionGroup": "Ballroom",
      "competitionOrder": 3,
      "exceptions": [
        {
          "organization": "NDCA",
          "tempoRange": { "min": 112.0, "max": 120.0 }
        }
      ]
    }
  ]
}
```

- `competitionOrder`: 1–5 for round dances; 0 or omit for extras.
- `exceptions`: organization-specific tempo overrides; omit if none.
- One object per style/org combo if the dance appears in multiple categories
  (e.g. Cha Cha has American Rhythm, International Latin, and Country instances).

#### Social Dance (no competition)

```json
{
  "id": "XYZ",
  "name": "My Social Dance",
  "meter": { "numerator": 4, "denominator": 4 },
  "blogTag": "my-social-dance",
  "instances": [
    {
      "style": "Social",
      "tempoRange": { "min": 96.0, "max": 140.0 }
    }
  ]
}
```

#### Performance Dance (no meter, no competition)

For pure performance dances (Jazz, Broadway, Bollywood, etc.):

```json
{
  "id": "XYZ",
  "name": "My Performance Dance",
  "meter": { "numerator": 1, "denominator": 1 },
  "instances": [
    {
      "style": "Performance",
      "tempoRange": { "min": 1.0, "max": 500.0 }
    }
  ]
}
```

#### Pattern / Social Dance (no meter, no competition)

For social dances with no fixed meter (line dances, choreographed partner dances):

```json
{
  "id": "PTN",
  "name": "Pattern",
  "meter": { "numerator": 1, "denominator": 1 },
  "instances": [
    {
      "style": "Social",
      "tempoRange": { "min": 1.0, "max": 500.0 }
    }
  ]
}
```

- Use `meter: { "numerator": 1, "denominator": 1 }` to signal "no fixed meter".
- Use `tempoRange: { "min": 1.0, "max": 500.0 }` to accept any tempo.
- Use `style: "Social"` (not `"Performance"`) if the dance is primarily a social dance.
- Omit `organizations`, `competitionGroup`, and `competitionOrder`.

---

### Step 3 — Add the Dance to `dancegroups.json`

**File:** `m4d/ClientApp/src/assets/content/dancegroups.json`

Add the new dance ID to an **existing group's** `danceIds` array, or create a **new group** if
this is the start of a new category.

#### Adding to an existing group

```json
{
  "name": "Other",
  "id": "MSC",
  "danceIds": ["BLU", "C2S", "MWT", "NC2", "PLK", "XYZ"]
}
```

#### Creating a new group

If a new thematic group is needed (not just a new dance within an existing group), add a new group object:

```json
{
  "name": "My New Group",
  "id": "MNG",
  "danceIds": ["XYZ"]
}
```

- Group IDs are also 3 uppercase characters and must be unique across all group IDs.
- `blogTag` is optional; add one if there is a dedicated blog tag for the group.

---

### Step 4 — Update `DanceController.cs` (new major navigation groups only)

**File:** `m4d/Controllers/DanceController.cs`

This step is **only required** when you are introducing a new top-level navigation page for a
new competition or thematic group. For individual dances or additions to existing groups, skip
this step — the generic `stats.FromName(dance)` path handles routing automatically.

Add a new `if` block in `DanceController.Index()` following the same pattern as the
Country block:

```csharp
if (string.Equals(dance, "pattern", StringComparison.OrdinalIgnoreCase))
{
    return Vue3(
        "Pattern Dances",
        "An overview of line dances and choreographed partner dances.",
        "pattern",                          // Vue page component name
        CompetitionGroup.Get("Pattern"),    // or a custom model
        danceEnvironment: true
    );
}
```

The string passed to `Vue3()` as the third argument must match the name of the Vue page
component registered in the client app.

---

### Step 5 — Add navigation link (new major groups only)

**File:** `m4d/ClientApp/src/components/MainMenu.vue`

If you added a new controller route in Step 4, also add a link to the main navigation menu:

```vue
<BDropdownItem href="/dances/pattern">Pattern</BDropdownItem>
```

Place it in the "Dances" dropdown alongside the existing entries (Ballroom, Swing, Tango, etc.).

---

### Step 6 — Update test data (if writing tests for the new dance)

Three pairs of test data files mirror the production `dances.json` and `dancegroups.json`:

- `DanceTests/TestData/test-dances.json`
- `DanceTests/TestData/test-groups.json`
- `m4dModels.Tests/TestData/test-dances.json`
- `m4dModels.Tests/TestData/test-groups.json`

Add the new dance entry to these files with the same structure as the production files, but you
may use a simplified `tempoRange` unless the test specifically exercises tempo logic.

---

### Step 7 — Reload the dance library

The dance library and its frontend cache are initialized once and held in memory. After editing
the JSON files you must invalidate both caches to pick up the changes without restarting.

**In production / staging — use the Admin panel:**

```
GET /Admin/ClearSongCache
```

This endpoint (requires `showDiagnostics` role) performs a full reload:

1. Calls `DanceStatsManager.ClearCache` → `LoadFromAzure` → `InitializeDanceLibrary`, which
   calls `DanceLibrary.Dances.Reset()` to re-read `dances.json` and `dancegroups.json`.
2. Calls `DanceMusicController.ClearJsonCache()` to clear the static `s_danceDatabaseCache`,
   so the updated dance list is re-sent to browsers on the next page request.
3. As part of `DanceStatsInstance.FixupStats`, any dance that does not yet have a database
   record is automatically created with a placeholder description and added to the search index.

> ⚠️ **`/Admin/ReloadDances` is NOT sufficient for new dances.** That endpoint only refreshes
> dance metadata (description, etc.) for dances already in `Instance.Map`. It does not
> re-read the JSON files and does not create new search-index entries.

**In local development:**

Restart the application. The `DanceLibrary.Dances` singleton and the controller-level
JSON cache are both cleared on startup.

---

### Zero-song dances and the `emptydance` view

After the cache reload, the new dance will be visible in navigation and the sitemap, but the
`DanceController` checks `SongCount` before rendering the full page:

```csharp
if (ds.SongCount == 0)
{
    UseVue = UseVue.No;
    return View("emptydance", ds);
}
```

Until at least one song has an upvote for the new dance, visiting `/dances/{dance-name}` will
show a placeholder "empty dance" page rather than the full Vue song-list page. **You do not
need a song before the dance is registered** in the system, indexed, or appears in search —
but you do need at least one rated song for the full detail page to render.

The "empty dance" view also does **not** provide a description edit control. If you want to
write a description for the dance before it has songs, you must rate at least one song for it
first to get to the full detail page where editing is available.

---

## What Updates Automatically (No Changes Needed)

The following are fully driven by the JSON data and require no manual updates:

| Component                                                  | How it auto-discovers                                 |
| ---------------------------------------------------------- | ----------------------------------------------------- |
| `DanceType`, `DanceInstance`, `DanceGroup` (C#)            | Deserialized from JSON on startup                     |
| `CompetitionCategory` / `CompetitionGroup` (C#)            | Built dynamically from `instances[].competitionGroup` |
| `DanceDatabase`, `DanceType`, `DanceInstance` (TypeScript) | Deserialized from JSON injected by the server         |
| Sitemap (`SiteMapDances`)                                  | Iterates `Dances.Instance.AllDances`                  |
| Search / song filter (`DanceQuery`, `SongFilter`)          | Uses `Dances.Instance.DanceFromName()`                |
| `DanceStatsManager` statistics                             | Loads from `DanceMusicContext` after DB refresh       |

---

## Quick Checklist

- [ ] Chose a unique 3-character uppercase ID
- [ ] Added dance object to `m4d/ClientApp/src/assets/content/dances.json`
- [ ] Added dance ID to appropriate group in `m4d/ClientApp/src/assets/content/dancegroups.json`
- [ ] _(If new group)_ Created new group object in `dancegroups.json`
- [ ] _(If new group)_ Added `if` block to `DanceController.Index()` for the group landing page
- [ ] _(If new group)_ Added menu link to `MainMenu.vue`
- [ ] _(If writing tests)_ Updated test JSON data files
- [ ] Ran `/Admin/ClearSongCache` (or restarted the app in dev) to reload the dance library

---

## Example: Adding the "Pattern" Dance Type

The "Pattern" dance type covers line dances and choreographed partner dances. Like Performance
dances it uses `meter: 1/1` and a wide-open tempo range, but uses `style: "Social"` because
these are primarily social dances.

### `dances.json` entry

```json
{
  "id": "PTN",
  "name": "Pattern",
  "meter": { "numerator": 1, "denominator": 1 },
  "synonyms": ["Line Dance", "Choreographed Partner Dance"],
  "instances": [
    {
      "style": "Social",
      "tempoRange": { "min": 1.0, "max": 500.0 }
    }
  ]
}
```

### `dancegroups.json` — add to the "Other" group

Add `PTN` to the `MSC` group's `danceIds`:

```json
{
  "name": "Other",
  "id": "MSC",
  "danceIds": ["BLU", "C2S", "MWT", "NC2", "PLK", "PTN"]
}
```

No changes to `DanceController.cs` or `MainMenu.vue` are needed.

After editing both JSON files, run `/Admin/ClearSongCache` to pick up the changes. The dance
will initially show the "empty dance" placeholder page until at least one song is rated for it.
