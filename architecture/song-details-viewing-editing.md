# Song Details: Viewing and Editing

## Overview

The song details page (`/song/details?id=<guid>`) renders a fully interactive song card where anonymous visitors can view song data and authenticated users can vote on dances, edit metadata, and add tags. Admins gain additional controls over the raw history and can undo individual users' edits.

---

## Server-Side Entry Point

**`m4d/Controllers/SongController.cs` — `Details` action**

1. Spider/bot check via `CheckSpiders()`.
2. Search service availability check — returns a minimal unavailable page if unhealthy.
3. `SongIndex.FindSong(id)` retrieves the song; if not found, `FindMergedSong` checks whether the ID was merged into another song and returns a permanent redirect if so.
4. `GetSongDetails(song)` builds a `SongDetailsModel`:
   - `SongHistory` — anonymized via `UserMapper.AnonymizeHistory` (replaces PII with display names).
   - `Filter` — current URL filter state (`SongFilterSparse`).
   - `UserName` — current authenticated user's name (or null).
5. The model is serialized to JSON and injected as `model_` into the Vue page shell via `Vue3(...)`.

---

## Client-Side Entry Point

**`src/pages/song/App.vue`**

Deserializes the injected `model_` JSON string using `TypedJSON` into a `SongDetailsModel` instance, then renders `<SongCore :model="model" />` inside a `<PageFrame>`.

---

## Core Data Models

### `SongDetailsModel`

The root view model passed from server to client.

| Field         | Type          | Purpose                                                |
| ------------- | ------------- | ------------------------------------------------------ |
| `songHistory` | `SongHistory` | The full event-sourced property log for the song       |
| `filter`      | `SongFilter`  | Active search filter (preserved across navigation)     |
| `userName`    | `string`      | Logged-in user's name; `null` for anonymous            |
| `created`     | `boolean?`    | Set when the song was just created (unused in viewing) |

### `SongHistory`

An append-only log of `SongProperty` entries. The song's current state is derived by replaying these properties in order.

### `SongProperty`

A name/value pair with a structured naming convention (see `SongUploadFormat.md`). Key prefixes include:

- `Title`, `Artist`, `Tempo`, `Length`, `Sample` — basic metadata fields
- `Album:nn`, `Purchase:nn:S` — album/purchase indexed fields
- `Tag+` / `Tag-` — add/remove tags
- `Tag+:DANCEID=` — dance-specific tags
- `DanceRating=DANCEID+weight` — dance vote weights
- `.Create`, `.Edit` — action markers

### `Song`

A computed view of `SongHistory`; built by `Song.fromHistory(history, userName)`. Provides computed properties like `danceRatings`, `tags`, `albums`, `tempo`, `artist`, etc. Used purely for display — never mutated directly.

### `SongEditor`

The client-side state manager for the editing workflow. Initialized from a `SongHistory` and a username.

- Maintains a mutable `properties: SongProperty[]` list that starts as a copy of `history.properties`.
- Tracks `modified: boolean` whenever new properties are appended.
- Computes the `song` getter (current `Song`) and `editHistory` getter (only the newly appended properties) on demand.
- `saveChanges()` — PATCH `/api/song/{id}` with only `editHistory` (new properties since load).
- `create()` — POST `/api/song/` with the full `songHistory`.
- `revert()` — discards all pending changes and resets to the initial state.
- `commit()` — called after a successful save; resets `modified = false` and updates the baseline.
- Admin mode (`admin = true`) uses PUT instead of PATCH, sending the entire history.

---

## `SongCore.vue` — Top-Level Component

**Props:**

- `model: SongDetailsModel` — the server-provided model.
- `startEditing?: boolean` — if true, enters edit mode immediately (used for the "Add Song" flow).
- `creating?: boolean` — if true, uses `create()` instead of `saveChanges()`, and button reads "Add Song".

**Key reactive state:**

- `songStore` — a `ref<Song>` computed from the initial history; updated after a save.
- `editor` — a `ref<SongEditor | null>`; null when not authenticated.
- `edit` — a `ref<boolean>` controlling whether fields show as editable inputs.

**Derived computeds:**

- `song` — returns `editor.value.song` when editing (live view of pending changes), else the stable `songStore`.
- `editing` — true when `modified || edit`.
- `showSave` — true when `(modified && checkDances) || (isAdmin && edit)`.
- `adminProperties` — a writable computed exposing the raw property list as newline-delimited text for the admin textarea.

**Unsaved-changes guard:**
`leaveWarning` is registered on `beforeunload` and warns the browser when `modified` is true.

**Toast notification:**
The first time `modified` becomes true, a "Don't Forget!" bootstrap-vue-next toast is created at `top-center` to prompt saving.

---

## Layout Sections (Template)

### Header Row

- **Title** (italic) and **Artist** are rendered via `<FieldEditor>` — clicking "Edit" reveals text inputs.
- **Like button** (`<SongLikeButton>`) — toggling calls `editor.toggleLike()`, which appends a `Like` property.
- **Action buttons** (right column, visible when `editing || context.canTag`):
  - Environment links (admin only): Production / Test / Local shortcuts.
  - **Delete** (admin only, not editing) — links to `/song/delete?id=`.
  - **Cancel** / **Edit** (toggle).
  - **Save Changes** / **Add Song** (shown when `showSave`).

### Middle Row

- **Purchase section** (`<PurchaseSection>`) — 4 columns, shows streaming links.
- **Tag list** (`<TagListEditor>`) — song-level tags. Users can add/remove tags from a tag database; changes queue `Tag+`/`Tag-` properties via `SongEditor.addProperty`.
- **Audio sample** (if `song.hasSample`) — HTML5 `<audio>` element.
- **Comments** (`<CommentEditor>`

### Dance + Stats Row

- **Dances** (`<DanceDetails>`):
  - Shows a `<BCard>` listing each `DanceRating` sorted by descending weight.
  - Each row: vote buttons (`<DanceVote>`), dance name link, and per-dance tags (`<TagListEditor context="Dance">`).
  - Admin "Delete dance" button removes the dance tag via `onDeleteDance` → `editor.addProperty(deleteTag, ...)`.
- **"Add Dance Style"** button — opens the `<DanceChooser>` modal.
- **Song stats** (`<SongStats>`) — editable table of Tempo (BPM), Length (seconds), and EchoNest-derived Beat/Energy/Mood values.
- **"Undo My Changes"** button — shown when user has prior changes; triggers a confirmation modal then submits a hidden form to `POST /song/undoUserChanges`.

### Albums Row

- **Album list** (`<AlbumList>`) — linked album names with track numbers and purchase logos; admins can delete albums in edit mode.
- **Track list** (`<TrackList>`) — admin-only; search for tracks to associate new albums.
- **Admin Edit** textarea — raw `SongProperty` list editable as newline-delimited text via `adminProperties` computed.
- **Undo User Edits** — admin-only per-user undo buttons.
- **Update Services** — admin-only button linking to `/song/UpdateSongAndServices`.
- **History log** (`<SongHistoryLog>`) — admin-only; shows individual properties with move/delete/insert controls.
- **Song history viewer** (`<SongHistoryViewer>`) — shows a filtered change summary visible to all users. See [Song History Viewer](#song-history-viewer) below for what is included.

### Dance Chooser Modal

`<DanceChooser>` filters out already-added dance IDs and pre-filters by song tempo/meter.
Selecting a dance calls `addDance(danceId, persist, familyTag)`:

1. If only one style family exists for the dance, auto-selects it.
2. For multi-family dances with no family tag provided, votes without a family tag.
3. Calls `editor.danceVote(new DanceRatingVote(danceId, VoteDirection.Up, [familyTag]))`.

### Song History Viewer

`<SongHistoryViewer>` renders a `<BCard>` listing a filtered change history. It accepts two props: `history: SongHistory` and `authenticated: boolean`.

**Toggle — "Include automated":**
Authenticated users see a switch in the card header (with a CPU icon). When off (default), `SongHistory.userChanges` is used. When on, `SongHistory.inclusiveChanges` is used, which includes batch/algorithmic blocks alongside human edits.

**Layer 1 — `userChanges` / `inclusiveChanges`** (`SongHistory.ts`):
Iterates all properties grouped into edit blocks (each block starts with a `.Create` or `.Edit` action marker). A block is included when:

1. A `User` property is present (system/service edits without a user are excluded).
2. (`userChanges` only) The block is not a batch import — excludes `user === "batch|P"` or `user.startsWith("batch-")`.
3. The block contains at least one property whose `baseName` is one of: `Tag+`, `Tag-`, `Like`, `Comment+`, `Comment-`, `Tempo`.

Blocks from the same user on the same calendar day are merged into a single `SongChange` entry.

**Layer 2 — `SongChangeViewer` component** (`SongChangeViewer.vue`):
Within each `SongChange`, only properties whose `baseName` starts with `"Tag"` or `"Comment"`, or equals `"Tempo"`, are rendered as text rows. This means a like-only change displays just the heart icon (❤️/💔) with the user and date, and no property rows.

**What each entry shows:**

- A like/heart icon if the change includes a `Like` property (❤️ for liked, 💔 for disliked, ✏️ otherwise).
- `"Added by [user] on [date]"` for the song's `.Create` block; `"Changed by [user] on [date]"` for all others.
- One row per tag, comment, or tempo property in the block.
- Batch/algorithmic users are shown with friendly display names (e.g., "Spotify", "Amazon Music", "Tempo Bot", "Anonymous Import") and rendered as plain text (no profile link).

**What is intentionally excluded:** album edits, purchase links, dance rating weight changes, and any system/batch block when the toggle is off.

**Strike-through for removed tags:**
`SongHistory.activeTags` computes the set of song-level tag keys currently active (added and not subsequently removed). Tags shown in history that are no longer active are rendered with a strike-through via `TagViewer`. Dance-qualified tags (e.g., `Tag+:JIV=Modern:Style`) are explicitly excluded from this strike-through logic since they are not tracked in `activeTags`.

### Tag Modal

`<TagModal>` is opened for any tag chip click, passing a `TagHandler` for context-specific tag editing.

---

## Editing Flow

```text
User clicks "Edit"
    → edit = true
    → title/artist/tempo/etc. fields switch to <input>

User types in a field
    → FieldEditor emits "update-field"
    → SongCore.updateField() → editor.modifyProperty(name, value)
    → editor.modified = true
    → Toast appears

User clicks "Save Changes"
    → SongCore.saveChanges()
    → editor.saveChanges()  (PATCH /api/song/{id} with editHistory)
    → editor.commit() → modified = false
    → edit = false
```

---

## API Endpoints (Server)

All endpoints are in `m4d/APIControllers/SongController.cs`:

| Method  | URL                    | Auth          | Purpose                                       |
| ------- | ---------------------- | ------------- | --------------------------------------------- |
| `GET`   | `/api/song?search=...` | Anonymous     | Full-text search                              |
| `GET`   | `/api/song/{id}`       | Anonymous     | Fetch a single song's `SongHistory`           |
| `PATCH` | `/api/song/{id}`       | Authenticated | Append new edit properties (normal user save) |
| `PUT`   | `/api/song/{id}`       | Authenticated | Replace entire history (admin save)           |
| `POST`  | `/api/song/`           | Authenticated | Create a new song                             |

**PATCH vs PUT:**

- `PATCH` appends only `editHistory` (properties added since page load) — preserves existing data.
- `PUT` sends the entire history — used by admins for structural corrections.
- Both endpoints `DeanonymizeHistory` before writing, re-linking display names back to real user IDs.

---

## Permission Model

| Capability                | Anonymous | Authenticated | `canTag` | `canEdit` | Creator | Admin (`dbAdmin`) |
| ------------------------- | --------- | ------------- | -------- | --------- | ------- | ----------------- |
| View song details         | ✅        | ✅            | ✅       | ✅        | ✅      | ✅                |
| Vote on dances            | ❌        | ✅            | ✅       | ✅        | ✅      | ✅                |
| Add/remove own tags       | ❌        | ✅            | ✅       | ✅        | ✅      | ✅                |
| Remove system-only tags ² | ❌        | ✅            | ✅       | ✅        | ✅      | ✅                |
| Remove other users' tags  | ❌        | ❌            | ❌       | ✅        | ❌      | ✅                |
| Edit Tempo/Length         | ❌        | ❌            | ✅       | ✅        | ✅      | ✅                |
| Edit algo-set Tempo ¹     | ❌        | ✅            | ✅       | ✅        | ✅      | ✅                |
| Edit Title/Artist         | ❌        | ❌            | ❌       | ❌        | ✅      | ✅                |
| Delete dances             | ❌        | ❌            | ❌       | ❌        | ❌      | ✅                |
| Admin textarea edit       | ❌        | ❌            | ❌       | ❌        | ❌      | ✅                |
| Undo per-user edits       | ❌        | ❌            | ❌       | ❌        | ❌      | ✅                |
| Raw history log           | ❌        | ❌            | ❌       | ❌        | ❌      | ✅                |

Role hierarchy (additive): `dbAdmin` implies all other roles. `canEdit` implies `canTag`-level capabilities. Roles are resolved via `MenuContext` (injected from server into the page's JS environment).

¹ **"Edit algo-set Tempo"**: Any authenticated user may edit the tempo when it has only ever been set by an algorithmic source (e.g., EchoNest `batch-e`, Tempo Bot) and has never been overridden by a human user. Detected via `Song.isUserModified(PropertyType.tempoField)`. Implemented in `SongStats.vue` via `isSystemTempo`/`canOverrideTempo` computed props and `FieldEditor`'s `overridePermission` prop.

² **"Remove system-only tags"**: Any authenticated user may remove a tag whose most recent modification was by a batch/algorithmic user (pseudo or batch user). Computed in `SongHistory.systemTagKeys` by replaying the property log and tracking which tags were last touched by a system user. Surfaced in `TagListEditor.vue` as the "Remove System Tags:" section (shown only to non-`canEdit` users; `canEdit` users get the full "Remove Tags:" section instead).

Role checks are resolved via `MenuContext` (injected from server into the page's JS environment).

---

## Related Files

| File                                              | Purpose                                                                                      |
| ------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| `m4d/Controllers/SongController.cs`               | MVC controller, `Details` action, `GetSongDetails`                                           |
| `m4d/APIControllers/SongController.cs`            | REST API (GET/PATCH/PUT/POST)                                                                |
| `src/pages/song/App.vue`                          | Vue app entry point                                                                          |
| `src/pages/song/components/SongCore.vue`          | Top-level song details component                                                             |
| `src/pages/song/components/DanceDetails.vue`      | Dance ratings card                                                                           |
| `src/pages/song/components/SongStats.vue`         | Tempo/length/EchoNest table                                                                  |
| `src/pages/song/components/SongHistoryLog.vue`    | Admin raw history editor                                                                     |
| `src/pages/song/components/SongHistoryViewer.vue` | User-facing change summary; "Include automated" toggle                                       |
| `src/pages/song/components/AlbumList.vue`         | Album list with purchase links                                                               |
| `src/pages/song/components/TrackList.vue`         | Admin track search/import                                                                    |
| `src/pages/song/components/FieldEditor.vue`       | Inline view/edit field switcher                                                              |
| `src/components/TagListEditor.vue`                | Tag add/remove widget                                                                        |
| `src/components/TagViewer.vue`                    | Single tag chip; strike-through for removed tags                                             |
| `src/components/SongPropertyViewer.vue`           | Renders a single `SongProperty` (tempo, tag, or comment row)                                 |
| `src/components/SongChangeViewer.vue`             | Renders one change block (user + date + properties)                                          |
| `src/components/UserLink.vue`                     | User display name; links for humans, plain text for batch/algorithmic users                  |
| `src/models/SongDetailsModel.ts`                  | Root view model                                                                              |
| `src/models/SongEditor.ts`                        | Client-side edit state manager                                                               |
| `src/models/SongHistory.ts`                       | Event-sourced property log; `userChanges`, `inclusiveChanges`, `activeTags`, `systemTagKeys` |
| `src/models/SongProperty.ts`                      | Property name/value pair + `PropertyType` enum                                               |
| `src/models/UserQuery.ts`                         | User identity; friendly display names for all system users                                   |
| `src/models/Song.ts`                              | Computed song view derived from `SongHistory`                                                |
| `architecture/SongUploadFormat.md`                | Property serialization format reference                                                      |
| `architecture/VOTING_WITH_DANCE_FAMILIES.md`      | Dance family voting details                                                                  |
