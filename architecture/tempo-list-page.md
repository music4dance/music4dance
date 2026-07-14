# Tempo List Page (`tempo-list/App.vue`)

## Overview

The Tempo List page (`m4d/ClientApp/src/pages/tempo-list/App.vue`) is a reference page at
`/Home/Tempi` ("Dance Tempi" in the site map) that lets a visitor cross-reference dance tempos:
pick a subset of styles, dance types, meters, and organizations, and see a sortable table of every
matching dance with its BPM/MPM tempo range. It has no server-side filtering — the full dance
database ships to the client as `window.danceDatabaseJson`, and every checkbox interaction re-runs
the filter client-side against `DanceFilter`.

## Server-Side Wiring

- `HomeController.Tempi(styles, types, organizations, meters)`
  (`m4d/Controllers/HomeController.cs:111-127`) reads four `List<string>` query-string parameters
  and renders the generic Vue3 host view (`m4d/Views/Shared/Vue3.cshtml`) with:
  - component name `"tempo-list"` (resolves to this `App.vue`)
  - a `TempoListModel` (`m4d/ViewModels/TempoListModel.cs`) built from the query params via
    `ConvertParameter`, which turns an empty list into `null` (`HomeController.cs:129-132`)
  - `danceEnvironment: true`, which makes `Vue3.cshtml` include
    `_environmentWriter.cshtml` and emit `window.danceDatabaseJson`
- Because `TempoListModel` is passed as the page's `Model.Model`, `Vue3.cshtml` serializes it with
  `_jsonCamelCase` and assigns the *object itself* (not a JSON string) to the global `model_` — this
  page reads `model_` directly rather than calling `TypedJSON.parse(model_, ...)` the way most other
  Vue3 pages do.
- No `[Route]` attribute; the URL is the default MVC route, `/Home/Tempi?styles=...&types=...&meters=...&organizations=...`.

## Client-Side Data Flow

```text
window.danceDatabaseJson ──▶ safeDanceDatabase() ──▶ fullDB: DanceDatabase
                                                          │
                                          groups = fullDB.groups minus "Performance"
                                                          │
                                          danceDatabase = fullDB.filter({ groups })
                                                          │
                        ┌───────────────┬─────────────────┼───────────────┐
                        ▼               ▼                 ▼               ▼
                styleOptions/styles typeOptions/types  meterOptions/meters organizationOptions/organizations
                (from model.styles) (from model.types)  (hard-coded 2/4,   (from model.organizations)
                                                          3/4, 4/4 — always
                                                          all selected)
                        └───────────────┴─────────────────┴───────────────┘
                                                          │
                                    dances = computed(() => danceDatabase.filter(
                                      new DanceFilter({ styles, groups: types, meters, organizations })
                                    ).dances)
                                                          │
                                                          ▼
                                              <TempoList :dances="dances" />
```

`fullDB` unconditionally drops the "Performance" group (Jazz, Contemporary, Ballet, Broadway, Tap,
Hip-Hop, Bollywood, Disco, Freestyle) before anything else happens — those dances never appear on
this page regardless of filter selection. **The Type dropdown still lists a "Performance"
checkbox anyway**: `DanceDatabase.filter()` rebuilds its `.groups` getter by scanning
`this.dances` — the *original*, unfiltered dance list — instead of the `dances` it just computed
two lines above (`DanceDatabase.ts:116-125`), so the group list doesn't actually reflect what was
filtered out. Checking only "Performance" always yields zero rows.

### Filter state (`CheckedList.vue`)

Each of the four dropdowns is a `CheckedList` bound with `v-model` to an array of selected values:

| Dropdown | Prop `type` | Options source | Value shape |
| --- | --- | --- | --- |
| Style | `"Style"` | `danceDatabase.styles` (unique instance styles) | kebab-case string (`optionsFromText`/`wordsToKebab`) |
| Type | `"Type"` | `danceDatabase.groups` names | kebab-case string |
| Meter | `"Meter"` | hard-coded `[2/4, 3/4, 4/4]` | `Meter` instance (compared via `.equals()`, cast through `unknown` because `CheckboxValue` doesn't include arbitrary objects — see `INT-TODO` at `App.vue:32`) |
| Organization | `"Organization"` | `danceDatabase.organizations` | kebab-case string |

`CheckedList` shows a dropdown button whose label is "All \<Type\>s" / "No \<Type\>s" / the single
selected item's text / "`N` \<Type\>s", derived from comparing `model.value.length` against
`options.length` (`CheckedList.vue:13-30`). The "Select All" checkbox is tri-state
(`indeterminate` when some but not all options are checked).

Style/Type/Organization option lists are seeded from the server-provided `model_` values via
`buildList()` → `filterValid()` (`App.vue:46-57`), which silently drops any server-provided value
that isn't a valid kebab option (e.g. stale query-string values from a bookmarked link) rather than
erroring. **The Meter dropdown does not read `model.meters` at all** — `meters` is always
initialized to all three hard-coded options regardless of the `meters` query-string parameter, even
though the server-side `TempoListModel.Meters` is populated and available on `model_`. This is a
gap relative to the other three filters, not an intentional design choice.

### Filtering logic (`DanceFilter.reduce`, `DanceDatabase.ts`)

`DanceFilter` (`m4d/ClientApp/src/models/DanceDatabase/DanceFilter.ts`) is the single source of
truth for turning a filter selection into a dance list — per the project convention, nothing in
this page hand-rolls filter matching. For each `DanceType`:

1. `matchMeter` — the dance's `meter` must equal one of the selected `Meter`s (skipped if
   `meters` is `undefined`, but this page always passes a defined array).
2. `matchGroups` — the dance must belong to at least one selected group ("some", not "every" — a
   dance like Viennese Waltz that's in both the Waltz and Country groups matches if either is
   selected).
3. `matchOrganizations` — same "some" semantics against the dance's instances' organizations.
4. If all three pass, instances are narrowed to those whose `style` is in the selected `styles`
   list (`getMatchingInstances`); if the resulting instance list is empty, the whole dance is
   dropped (`type.reduce(instances)` is only called when `instances.length > 0`).

Because step 4 uses `types.value !== undefined` (an empty array still counts as "defined"),
deselecting every style, group, meter, or organization checkbox produces an **empty result set**,
not "show everything" — `TempoList.vue`'s `emptyTable` computed then renders the caption "Please
select at least one item from every drop-down" (`TempoList.vue:15-17`).

`matchOrganizations` (`DanceFilter.ts:44-49`) is `type.organizations.some((o) => this.organizations!.includes(o))`
whenever `this.organizations` is defined — which it always is on this page, since App.vue always
passes a `organizations` array (initially "every option"). `.some()` on an **empty** array is
always `false`, so any dance with no organization affiliation on any instance (most "Social"-style
dances: Cross-step Waltz, Lindy Hop, Argentine Tango, Bossa Nova, Charleston, etc.) is filtered out
even when every organization checkbox is checked. In the shipped content this drops the page's
default result set from all 41 non-Performance dances down to 23 — the "Select All" state for
Organization silently means "all dances that have at least one organization," not "all dances."
See the `"organization-less dances are hidden..."` test in `App.test.ts` for a pinned example.

## `TempoList.vue` (results table)

A `BTable` over `props.dances: DanceType[]`, sorted by name ascending by default
(`sortBy = [{ key: "name", order: "asc" }]`). Columns:

| Column | Content |
| --- | --- |
| Name | `<DanceName>` (links to `/dances/{seoName}`, shows synonyms) |
| Meter | `dance.meter.toString()`, e.g. `"3/4"` |
| BPM | `dance.tempoRange.toString()`, linked to `/song/advancedsearch?dances={id}&tempomin=...&tempomax=...&sortorder=Dances` (`defaultTempoLink`) |
| MPM | `dance.tempoRange.mpm(dance.meter.numerator)`, same tempo-search link |
| Type | Comma-joined group names, each linked to `/dances/{groupName}` — only the *first* group is used to build the link even when a dance belongs to multiple groups (`groupLink` reads `dance.groups?.[0]`) |
| Styles | Comma-joined style names; a style is only linked to `/dances/{kebab-style}` if its name contains a space (`style.indexOf(" ") !== -1`) — a quirk that means single-word styles like "Social" or "Country" render as plain text while "American Rhythm" links |

Sorting on BPM/MPM sorts by `tempoRange.min` (zero-padded to 4 integer digits via
`sortByFormatted`), not by the displayed string, so ranges sort numerically rather than
lexicographically.

## Testing

- `m4d/ClientApp/src/pages/tempo-list/__tests__/App.test.ts` — mounts the real page (via
  `loadTestPage`, real `bootstrap-vue-next` components, real dance content JSON as test data — see
  [[testing-patterns]] "Client-Side Testing Patterns") and exercises the filter pipeline both
  programmatically (assigning to the exposed `styles`/`types`/`meters`/`organizations` refs) and
  through genuine DOM checkbox interaction (`input.setValue(true/false)` — `trigger("click")`
  does not reliably flip a `BFormCheckboxGroup` checkbox in jsdom, which is why the equivalent
  interaction test in `CheckedList.test.ts` was previously left `test.skip`).
- `m4d/ClientApp/src/pages/tempo-list/components/__tests__/TempoList.test.ts` — unit tests for the
  results table: column content/links for a known dance, the empty-selection caption, and default
  sort order.
- `m4d/ClientApp/src/pages/tempo-list/components/__tests__/CheckedList.test.ts` — pre-existing;
  covers the dropdown label logic. Two interaction tests remain `test.skip` there for the same
  `trigger("click")` reason above; they were not converted to `setValue` as part of this pass since
  `CheckedList.vue` itself was out of scope for this round of coverage.

## Known Gaps / Follow-ups

- **"Select All" on Organization doesn't mean "all dances."** `DanceFilter.matchOrganizations`'s
  `.some()` over an empty `organizations` array is always `false`, so every organization-less
  "Social" dance is hidden by default. This is the single biggest gap in the page's *current*
  behavior — it silently cuts the default result set from 41 dances to 23 — and would need a fix
  in the shared `DanceFilter` class (e.g. treat "every option selected" as equivalent to
  `undefined`, or special-case an empty `type.organizations`), not just in this page.
- **The Type dropdown offers a "Performance" option that always yields zero rows**, because
  `DanceDatabase.filter()`'s `.groups` getter is computed from the pre-filter dance list (see
  above). Also shared logic — a fix belongs in `DanceDatabase.ts`, not `App.vue`.
- Meter query-string parameter (`?meters=3%2F4`) is silently ignored client-side (see above) — a
  bookmarked/shared link with a meter filter loses that part of the selection on load.
- `CheckedList.test.ts` has two `test.skip`ped interaction tests with a TODO blaming
  model/event handling; `setValue(true)` (proven out in this page's tests) resolves the same
  symptom and could unblock them.
- `App.vue:9-10` has a standing `TODO` to clean up the `CheckboxOptions` structures and consider
  disabling checkboxes that can't produce any results given the current selection (e.g. disable an
  organization once no dance in the current style/type/meter selection offers it).

## Related Code

| File | Purpose |
| --- | --- |
| `m4d/ClientApp/src/pages/tempo-list/App.vue` | Page: builds filter option lists, holds selection state, computes `dances` |
| `m4d/ClientApp/src/pages/tempo-list/components/CheckedList.vue` | Reusable multi-select dropdown used for all four filters |
| `m4d/ClientApp/src/pages/tempo-list/components/TempoList.vue` | Results table |
| `m4d/ClientApp/src/models/DanceDatabase/DanceFilter.ts` | Filter matching logic shared with other dance-filtering pages |
| `m4d/ClientApp/src/models/DanceDatabase/DanceDatabase.ts` | Dance/group/style/organization aggregation, `.filter()` |
| `m4d/ClientApp/src/models/CheckboxTypes.ts` | `CheckboxOption`/value conversion helpers (`optionsFromText`, `valuesFromOptions`, `textFromValues`) |
| `m4d/Controllers/HomeController.cs` | `Tempi` action |
| `m4d/ViewModels/TempoListModel.cs` | Server-side model matching the client `TempoListModel` interface |
| `m4d/Views/Shared/Vue3.cshtml`, `_environmentWriter.cshtml` | Generic Vue3 page host; emits `model_` and `window.danceDatabaseJson` |
