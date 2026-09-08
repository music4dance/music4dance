# Bookmarkable / Shareable Tool Links — Planning Doc

**Status:** Proposal — no implementation started.

## Goal

Several small "tool" pages already support deep-linking into a specific configuration via
query-string parameters — today this is used almost exclusively by the site owner hand-crafting
URLs for blog posts (see `blogmap.txt`: `/Home/counter`, `/Home/tempi`). We want ordinary visitors
to be able to reach the same result themselves: configure a page, then bookmark it or copy a link
to it, without needing to know the query-string format. The mechanism should be **extensible** —
adding this to a new tool page in the future should be a small, mechanical change, not a bespoke
design exercise each time.

## Current State

| Page | Route | Query params supported today | Reflected live in the URL as the user interacts? |
| --- | --- | --- | --- |
| Tempo List | `/Home/Tempi` | `styles`, `types`, `organizations`, `meters`, `columns` — all read on load ([[tempo-list-page]]) | No — read once at mount, never written back |
| Tempo Counter | `/Home/Counter` | `numerator`, `tempo`, `count` — read on load ([[tempo-counter-page]]); `epsilonPercent` ("strictness") has **no** param at all | No |
| Advanced Search | `/song/advancedsearchform` (form) → `/song/filtersearch?filter=...` (results) | `filter` (a serialized `SongFilter`) — the **form** already reads `?filter=` on load via `getQueryFilter()`, and `onSubmit` does a `history.replaceState` of the form's own URL before navigating to the results page | Only at submit time — never while editing |

So all three pages can already be *reached* via a URL that fully encodes their state — the gap is
entirely about **discoverability and ergonomics**:

- Tempo List / Tempo Counter: a visitor who configures the page has no way to get the URL for that
  configuration short of manually editing the address bar in the same format the blog posts use.
- Advanced Search is a variant of the same problem, one step removed: the **results** page's URL
  (`/song/filtersearch?filter=...`) is a perfectly good bookmark once you're on it, but there's no
  way to get there — or to get a link to a form-configuration that hasn't been submitted yet —
  without actually clicking Search.

## Requirements / Constraints

- Must work for **anonymous** visitors (blog readers aren't logged in) — rules out anything that
  depends purely on an authenticated, server-persisted record.
- Per `CLAUDE.md`'s filter-construction convention, existing serialized formats (`SongFilter`,
  `DanceQueryItem`, tag strings) must keep going through their class libraries — nothing here
  should hand-roll `filter=` string construction. Tempo List/Counter's plain scalar/array params
  aren't "filter strings" in that sense (no class library owns `styles`/`numerator`/etc. today),
  so a thin page-local URL builder is fine for those, matching how `HomeController` already reads
  them as plain `List<string>`/`int?`/etc.
- Should generalize: a future tool page should be able to opt in with a small, declarative amount
  of code, not a copy-pasted URL-serialization routine.
- Shouldn't degrade the pages' current behavior (e.g., Tempo List's silent "drop invalid seeded
  value" convention, or Advanced Search's `onSubmit` normalization of min/max order) — new
  URL-writing should feed off the same state those already read from.

## Options

### Option A — On-demand "Copy Link" button, no live URL sync

Each page computes its own shareable URL as a `computed` from its current state (reusing
`SongFilter.encodedQuery` for Advanced Search; a small page-local query-string builder for Tempo
List/Counter). A single shared `CopyLinkButton.vue` component takes a URL (string or computed) and
handles the clipboard write + a transient "Copied!" confirmation.

- **Pros:** Simplest to build and reason about — no `history.replaceState` churn, no interaction
  with browser back/forward behavior. Each page's URL-building stays page-local, which fits the
  project's "each page owns its own filter construction" convention.
- **Cons:** The address bar itself never reflects the current configuration, so none of the
  browser's *native* sharing mechanisms work correctly — hitting Ctrl+D, using a mobile browser's
  "Share" menu, or just copying the address bar all capture a stale/default URL instead of the
  configured one. The custom button becomes the *only* correct way to get a link, which is easy to
  miss.

### Option B — Live URL sync via `history.replaceState`, no dedicated button

A shared composable keeps the page's query string continuously up to date with its state (via
`watchEffect` + `history.replaceState`, the same primitive Advanced Search's `onSubmit` already
uses once). No new UI is added — the address bar itself becomes the shareable link at all times.

- **Pros:** Works with every native browser mechanism for free (bookmarking, address-bar copy,
  mobile share sheets) and needs no explicit user action beyond configuring the page. One shared
  implementation covers every page that adopts it.
- **Cons:** No in-page confirmation that "this is now shareable" — users have to already know to
  use their browser's own copy/bookmark affordance, which is weaker on mobile (address bar is
  often scrolled out of view). Rapid-fire `replaceState` calls (e.g., every keystroke on Advanced
  Search) are cheap individually but worth a light debounce so history-adjacent browser extensions
  or dev tools don't see excessive churn.

### Option C — Combine A and B (recommended)

Use Option B's live sync as the underlying source of truth (so native browser sharing/bookmarking
always works correctly), **and** add Option A's `CopyLinkButton` as a direct, discoverable
affordance that just copies `window.location.href` — since B keeps that value current, the button
needs no page-specific URL-building logic of its own, only an optional `url` override for the one
case where the shareable link *isn't* the current page's own address (Advanced Search's form wants
to share the **results** link, not the form's own URL — see below).

- **Pros:** Gets both the "just works with the browser" behavior and a one-click, visibly-confirmed
  action for users who wouldn't think to use browser-native sharing. The button's implementation
  stays trivial (copy current `href`, or an explicit override) because the sync layer already did
  the hard part.
- **Cons:** Marginally more to build than either option alone (a composable *and* a component), but
  each piece is small and each is independently reusable for future pages.

### Option D — (Search-specific, complementary) Surface the existing Saved Searches "Name" field

Independent of A/B/C: `m4dModels.Search` already has a `Name` column that's "currently unused in
UI" (see [[saved-searches]]), and every Advanced Search submission is already auto-logged there for
authenticated users. A relatively small addition — letting a user assign/edit a name for one of
their entries on `/searches` — turns that existing, account-bound history into genuine curated
bookmarks, with no query-string plumbing at all.

- **Pros:** Reuses infrastructure that already exists and already runs on every search; gives named
  (not just opaque-URL) bookmarks, which neither A, B, nor C provide.

- **Cons:** Authenticated users only — does nothing for the anonymous-blog-reader case that
  motivated this request, and doesn't generalize to Tempo List/Counter (which have no server-side
  persistence at all today). This is a complement to A/B/C for logged-in users, not a substitute.

## Recommendation

**Option C** as the general-purpose mechanism, applied to all three pages, with **Option D** noted
as a separate, smaller follow-up specific to the search/logged-in case (not required to satisfy the
original request).

Concretely:

1. **`useUrlQuerySync` composable** (new, e.g. `m4d/ClientApp/src/composables/useUrlQuerySync.ts`):
   takes a small declarative map of `{ paramName: { ref, serialize, deserialize? } }` (or simply a
   `computed<URLSearchParams>` built by the caller — the exact shape is an implementation detail to
   settle when building this) and runs a `watchEffect` that calls `history.replaceState` whenever
   the computed query string changes. It only owns the **write** direction; each page keeps reading
   its initial state from `model_`/`getQueryFilter()` exactly as it does today, so none of the
   existing "silently drop invalid seeded values" logic (Tempo List's `filterValid`,
   `filterValidMeters`) needs to move or change.
2. **`CopyLinkButton.vue`** (new, shared component): defaults to copying `window.location.href`;
   accepts an optional `url` prop for the one case that needs to point elsewhere. Uses
   `navigator.clipboard.writeText` with a brief visible confirmation (a `bootstrap-vue-next`
   tooltip or toast — reuse whichever the project already leans on elsewhere for transient
   feedback, or a plain `BTooltip` shown for ~2s if there's no existing pattern). The Web Share API
   (`navigator.share`) could be layered on as a mobile-friendly enhancement (feature-detected,
   falling back to clipboard copy) but isn't required for a first pass.
3. **Per-page wiring:**
   - **Tempo List** — sync `styles`/`types`/`organizations`/`meters` (the four `CheckedList`
     selections) and `visibleColumns` (the column chooser, currently URL-seedable but not
     URL-writing — see [[tempo-list-page]] "Column chooser") into the query string; drop a
     `CopyLinkButton` near the filter row.
   - **Tempo Counter** — sync `beatsPerMeasure` → `numerator`, `beatsPerMinute` → `tempo`,
     `countMethod` → `count`, **and add `epsilonPercent` as a new query param** (closing the gap
     noted in [[tempo-counter-page]] "Known Gaps") so a shared link reproduces the strictness
     setting too; drop a `CopyLinkButton` near the counter controls.
   - **Advanced Search (form)** — sync the existing `songFilter.value.encodedQuery` into the form's
     own `?filter=` param (this already happens once, in `onSubmit` — the composable just makes it
     continuous instead of submit-time-only). The `CopyLinkButton` here should use the `url` override
     to point at the **results** link
     (`` `${location.origin}/song/filtersearch?filter=${songFilter.encodedQuery}` ``), since that's
     the link worth sharing — the form's own URL is only useful to someone who wants to keep editing.
   - **Advanced Search (results) / `SearchHeader.vue` & `SongLibraryHeader.vue`** — no live-sync
     needed here (the results page's own URL is already the correct, current link); just add a bare
     `CopyLinkButton` (default `window.location.href`) next to the existing "Change" button.
4. **Future pages** adopt the same two pieces: wire their state into `useUrlQuerySync`, drop in
   `CopyLinkButton`. No new shared code should be needed per page beyond that.

### Testing impact

- `useUrlQuerySync` gets its own unit tests (params reflect state changes, debounced/consolidated
  writes, doesn't fight with the initial server-seeded state).
- `CopyLinkButton` gets a unit test for the clipboard-write + confirmation behavior (mock
  `navigator.clipboard`).
- Existing page tests (`tempo-list/__tests__/App.test.ts`, and any new Tempo Counter tests written
  to close its current zero-coverage gap — see [[tempo-counter-page]] "Testing") will need
  `window.history.replaceState` either left as the real jsdom implementation (cheap, no mocking
  needed) or spied on where a test wants to assert the URL was updated.

## Open Questions

- Exact debounce interval (if any) for the live sync on Advanced Search, which has far more
  independently-editable fields than Tempo List/Counter.
- Whether `CopyLinkButton`'s confirmation UI should be a tooltip, toast, or inline text change —
  worth checking if the project has an established transient-feedback pattern elsewhere before
  introducing a new one.
- Whether Option D (naming a saved search) is worth doing now alongside this, or tracked as a
  separate, later piece of work — it touches `SearchesController`/`_SearchesCore.cshtml`, not the
  Vue tool pages, so it can proceed independently on its own timeline.
