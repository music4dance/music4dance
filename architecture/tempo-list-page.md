# Tempo List Page (`tempo-list/App.vue`)

## Overview

The Tempo List page (`m4d/ClientApp/src/pages/tempo-list/App.vue`) is a reference page at
`/Home/Tempi` ("Dance Tempi" in the site map) that lets a visitor cross-reference dance tempos:
pick a subset of styles, dance types, meters, and organizations, and see a sortable table of every
matching dance with its BPM/MPM tempo range. It has no server-side filtering — the full dance
database ships to the client as `window.danceDatabaseJson`, and every checkbox interaction re-runs
the filter client-side against `DanceFilter`.

## Server-Side Wiring

- `HomeController.Tempi(styles, types, organizations, meters, columns)`
  (`m4d/Controllers/HomeController.cs:111-127`) reads five `List<string>` query-string parameters
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
- No `[Route]` attribute; the URL is the default MVC route, `/Home/Tempi?styles=...&types=...&meters=...&organizations=...&columns=...`.
  `columns` seeds `TempoList.vue`'s column chooser (see "Column chooser" below) rather than the four
  page-level filters — it's forwarded through `App.vue` as `model.columns` → `TempoList`'s
  `initial-columns` prop, letting a custom column set (e.g. the normally-hidden Range column) be
  linked to directly. Column keys are `TempoList.vue`'s `chooseableColumns` keys: `meter`, `bpm`,
  `mpm`, `groupName` (Type), `styles`, `validationRange` (Range). E.g. to show only Meter, BPM, and
  Range: `/Home/Tempi?columns=meter&columns=bpm&columns=validationRange`.

## Client-Side Data Flow

```text
window.danceDatabaseJson ──▶ safeDanceDatabase() ──▶ fullDB: DanceDatabase
                                                          │
                                    timedDances = fullDB.dances without tempoRange.isInfinite
                                                          │
                          danceDatabase = new DanceDatabase({ dances: timedDances, groups: fullDB.groups })
                                                .filter(new DanceFilter({}))   // recomputes .groups
                                                          │
                        ┌───────────────┬─────────────────┼───────────────┐
                        ▼               ▼                 ▼               ▼
                styleOptions/styles typeOptions/types  meterOptions/meters organizationOptions/organizations
                (from model.styles) (from model.types)  (hard-coded 2/4,   (from model.organizations)
                                                          3/4, 4/4 — always
                                                          all selected)
                        └───────────────┴─────────────────┴───────────────┘
                                                          │
                        dances = computed(() => danceDatabase.filter(new DanceFilter({
                          styles, groups: types, meters,
                          organizations: selectedOrgs.length === organizationOptions.length
                            ? undefined       // "every org checked" == "no restriction"
                            : selectedOrgs,
                        })).dances)
                                                          │
                                                          ▼
                                              <TempoList :dances="dances" />
```

`fullDB.dances` is filtered down to `timedDances` by `!d.tempoRange.isInfinite` before anything
else happens, so any dance with no real tempo never appears on this page regardless of filter
selection. This used to be done by dropping the "Performance" *group* instead — which happened to
exclude the 9 actual Performance dances (Jazz, Contemporary, Ballet, Broadway, Tap, Hip-Hop,
Bollywood, Disco, Freestyle) but missed **Pattern** (`PTN`), a "Social"-style dance filed under the
"Other" group that also has no real tempo (its one instance carries the same `{min: 1, max: 500}`
placeholder range — `TempoRange.isInfinite` — as every Performance dance). Filtering by
`tempoRange.isInfinite` catches both categories directly instead of relying on group membership as
a proxy.

### Name filter (`NameFilterInput.vue`)

A fifth filter row, directly below the four `CheckedList` dropdowns: a free-text box (search-icon
`BInputGroup`) bound to a `nameFilter` ref. `App.vue`'s `dances` computed applies it last, via
`DanceDatabase.filterByName(danceDatabase.filter(filter).dances, nameFilter.value)` — the
`DanceFilter` pass narrows by style/type/meter/organization first, then the name filter narrows
the *result* by substring match against each dance's `hasString()` (id, name, synonyms,
searchonyms, case-insensitive, letters-only normalized), same as it ANDs with everything else.

`NameFilterInput.vue` (`m4d/ClientApp/src/components/`) is a small shared component — just the
`BInputGroup`/`BFormInput`/search-icon markup behind a `v-model` — extracted from this same
pattern in `dance-index`'s `DanceTable.vue`. It does not call `DanceDatabase.filterByName` itself;
per the project's filter-construction convention, each page still owns the call to the class
library's static method and decides what to filter. `DanceChooser.vue` has a near-identical inline
input but was left as-is since it's a modal with its own layout constraints, not currently sharing
a component tree with either page above.

### Filter state (`CheckedList.vue`)

Each of the four dropdowns is a `CheckedList` bound with `v-model` to an array of selected values:

| Dropdown | Prop `type` | Options source | Value shape |
| --- | --- | --- | --- |
| Style | `"Style"` | `danceDatabase.styles` (unique instance styles) | kebab-case string (`optionsFromText`/`wordsToKebab`) |
| Type | `"Type"` | `danceDatabase.groups` names | kebab-case string |
| Meter | `"Meter"` | hard-coded `[2/4, 3/4, 4/4]` | `Meter` instance (compared via `.equals()`, cast through `unknown` because `CheckboxValue` doesn't include arbitrary objects — see `INT-TODO` at `App.vue:39`) |
| Organization | `"Organization"` | `danceDatabase.organizations` | kebab-case string |

`CheckedList` shows a dropdown button whose label is "All \<Type\>s" / "No \<Type\>s" / the single
selected item's text / "`N` \<Type\>s", derived from comparing `model.value.length` against
`options.length` (`CheckedList.vue:13-30`). The "Select All" checkbox is tri-state
(`indeterminate` when some but not all options are checked).

Style/Type/Organization option lists are seeded from the server-provided `model_` values via
`buildList()` → `filterValid()` (`App.vue:53-64`), which silently drops any server-provided value
that isn't a valid kebab option (e.g. stale query-string values from a bookmarked link) rather than
erroring. The Meter dropdown is seeded the same way via `filterValidMeters()`, a `Meter`-specific
sibling of `filterValid()`: it matches server-provided strings (e.g. `"3/4"`) against each
hard-coded option's `text`, then returns the corresponding `Meter` instances rather than strings
(so equality later goes through `Meter.equals()`, not string comparison) — same "silently drop
anything invalid" behavior as the other three filters.

### Result counts / cross-filtering (`counts` prop)

Each dropdown's options are annotated with a live result count (`"Waltz (12)"`) and zero-count
options are grayed out — but stay fully checkable, rather than being `disabled`. This is the
faceted-search pattern (Amazon/Etsy/Google Shopping-style: show the count, mute what's currently
empty) chosen over hard-disabling unavailable options, since disabling hides the "why," while a
`(0)` explains it and still lets a user build up a selection in any order.

`App.vue` computes four count arrays — `styleCounts`, `typeCounts`, `meterCounts`,
`organizationCounts` — each parallel to its option list. For each option, the count answers "how
many dances would match if I additionally applied *just this option*ʼs facet, holding the other
three facets at their current selection" — via `countMatching()`, which re-runs
`danceDatabase.filter()` (then `filterByName()`, so the name filter narrows counts too) with a
`DanceFilter` built from the other three facets' current selection plus this one option alone.
Deliberately **not** dependent on what else is checked within the *same* facet — checking/
unchecking a second Style doesn't change any Style option's own count, only the other three
dropdowns'. This means one extra `danceDatabase.filter()` pass per option (a few dozen per
keystroke), which is cheap at this dataset's size; there's no memoization beyond each count array's
own `computed`.

Two gotchas the counts computeds have to account for that the main `dances` computed already
handled correctly:

- **Text vs. kebab value.** `DanceFilter.styles`/`.groups`/`.organizations` compare against the
  original display text (e.g. `"International Standard"`), not the kebab `CheckboxOption.value`
  (`"international-standard"`) used for `v-model`/URL round-tripping — the same distinction
  `textFromValues()` already resolves for the *selected* arrays. `Meter` is the exception: its
  options were never kebab-encoded, so `option.value` is already the real `Meter` instance to
  filter on. (Building `styleCounts`/`typeCounts`/`organizationCounts` off `option.value` instead
  of `option.text` was an actual bug caught by the "per-option counts" test below — every count
  came back `0` because no dance's style/group/organization text ever equals a kebab string.)
- **Organization counts use the strict per-organization count, not the "select all → `undefined`"
  normalization** the main `dances` computed applies (see below) — an organization's count answers
  "how many dances are affiliated with just this one," which needs the narrow, defined-list
  semantics, not "no restriction."

`CheckedList.vue` takes the count array as an optional `counts?: number[]` prop (parallel to
`options`, omitted entirely by the column chooser use in `TempoList.vue`, which renders unchanged).
When present, it overrides `BFormCheckboxGroup`'s `#option` scoped slot (`{ text, value, disabled }`
— bootstrap-vue-next renders this in place of each option's default label content, so the
checkbox/input/model wiring is untouched) to render `text` plus a muted `"(N)"` suffix, and applies
`text-muted` to the whole label when that option's count is `0`. The slot only hands back `value`,
not an index, so `CheckedList` recovers the count by `options.findIndex((o) => o.value === value)`
— safe by reference equality, since the slot's `value` is always the exact object `App.vue` put in
`options[i].value` (kebab string or, for Meter, the literal `Meter` instance), never a clone.

### Filtering logic (`DanceFilter.reduce`, `DanceDatabase.ts`)

`DanceFilter` (`m4d/ClientApp/src/models/DanceDatabase/DanceFilter.ts`) is the single source of
truth for turning a filter selection into a dance list — per the project convention, nothing in
this page hand-rolls filter matching. For each `DanceType`:

1. `matchMeter` — the dance's `meter` must equal one of the selected `Meter`s (skipped if
   `meters` is `undefined`, but this page always passes a defined array).
2. `matchGroups` — the dance must belong to at least one selected group ("some", not "every" — a
   dance like Viennese Waltz that's in both the Waltz and Country groups matches if either is
   selected).
3. `matchOrganizations` — same "some" semantics against the dance's instances' organizations (see
   below for why this page normalizes "every organization checked" to `undefined` before this
   step runs, rather than passing the full option list through).
4. If all three pass, instances are narrowed to those whose `style` is in the selected `styles`
   list (`getMatchingInstances`); if the resulting instance list is empty, the whole dance is
   dropped (`type.reduce(instances)` is only called when `instances.length > 0`).

Because step 4 uses `types.value !== undefined` (an empty array still counts as "defined"),
deselecting every style, group, meter, or organization checkbox produces an **empty result set**,
not "show everything" — `TempoList.vue`'s `emptyTable` computed then renders the caption "Please
select at least one item from every drop-down" (`TempoList.vue:15-17`).

`matchOrganizations` (`DanceFilter.ts:44-47`) is `type.organizations.some((o) => this.organizations!.includes(o))`
whenever `this.organizations` is defined. `.some()` on an **empty** array is always `false`, so a
dance with no organization affiliation on any instance (most "Social"-style dances: Cross-step
Waltz, Lindy Hop, Argentine Tango, Bossa Nova, Charleston, etc.) can never match a *defined*
`organizations` filter, no matter what it contains — this is intentional/correct behavior for a
deliberate, narrow selection (e.g. "NDCA only" genuinely shouldn't surface un-sanctioned dances),
but it breaks if the caller passes the full option list to mean "no restriction," since a fully
populated array is still a *defined* filter. App.vue's `dances` computed handles this by
normalizing "every organization checkbox is checked" to `organizations: undefined` before building
the `DanceFilter`, rather than passing the explicit list through — see the comment there. This fix
is deliberately scoped to the page, not `DanceFilter` itself: `DanceFilter` is shared with
`DanceDeltas.vue` and the `DanceDatabaseFiltering.test.ts` fixtures, which rely on a *specific*
organization selection genuinely excluding unaffiliated dances (e.g. `organizations: ["NDCA"]`
should not surface a Social-only dance just because it has no organization at all).

## `TempoList.vue` (results table)

A `BTable` over `props.dances: DanceType[]`, sorted by name ascending by default
(`sortBy = [{ key: "name", order: "asc" }]`). Columns:

| Column | Content |
| --- | --- |
| Name | `<DanceName>` (links to `/dances/{seoName}`, shows synonyms, `show-blog-link` renders a "Blog Posts" icon link — see "Blog link" below) |
| Meter | `dance.meter.toString()`, e.g. `"3/4"` |
| BPM | `dance.tempoRange.toString()`, linked to `/song/advancedsearch?dances={id}&tempomin=...&tempomax=...&sortorder=Dances` (`defaultTempoLink`) |
| MPM | `dance.tempoRange.mpm(dance.meter.numerator)`, same tempo-search link |
| Type | Comma-joined group names, each linked to `/dances/{groupName}` — only the *first* group is used to build the link even when a dance belongs to multiple groups (`groupLink` reads `dance.groups?.[0]`) |
| Styles | Comma-joined style names; a style is only linked to `/dances/{kebab-style}` if its name contains a space (`style.indexOf(" ") !== -1`) — a quirk that means single-word styles like "Social" or "Country" render as plain text while "American Rhythm" links |
| Range (hidden by default) | `dance.validationRange?.toString()` — see "Range column / DanceValidation" below |

Sorting on BPM/MPM sorts by `tempoRange.min` (zero-padded to 4 integer digits via
`sortByFormatted`), not by the displayed string, so ranges sort numerically rather than
lexicographically.

**BPM/MPM/Styles react to the current filter selection; Type now does too.** All three of
BPM/MPM/Styles are derived from `dance.instances` (`tempoRange` and `styles` are getters that
fold/map over `this.instances`), and `DanceFilter.reduce()` narrows `instances` to just the
selected styles before returning a dance — so e.g. with only "American Rhythm" selected, Rumba's
BPM/MPM/Styles columns show just that one instance's range and style, not the union across all its
instances. `Type`, by contrast, is a plain `dance.groups` field that `DanceType.reduce()` used to
clone unchanged (via `assign()`), so a dance in multiple groups (e.g. Viennese Waltz, in both Waltz
and Country) always showed every group it belongs to regardless of the Type selection.
`DanceFilter.reduce()` (`DanceFilter.ts:18-35`) now narrows `.groups` the same way it narrows
`.instances`: after building the reduced `DanceType`, it filters `.groups` down to the ones that
are also in `this.groups` (when a group filter is set), so Viennese Waltz's Type column shows just
"Waltz" once the Type filter is narrowed to Waltz.

### Range column / `DanceValidation`

`DanceValidation` (`m4d/ClientApp/src/models/DanceDatabase/DanceValidation.ts`) mirrors
`DanceLib/DanceValidation.cs` — the `doubleTempoIfBelow`/`halveTempoIfAbove` sanity-check
thresholds documented in `architecture/tempo-validation-rules.md`, used server-side to catch
Spotify/EchoNest half-time/double-time tempo detection errors. It's currently only populated on
Salsa's `"Social"` instance in `dances.json` (`doubleTempoIfBelow: 120`, `halveTempoIfAbove: 250`),
but more dances are expected to gain it over time.

`DanceInstance.validationRange` turns the two thresholds into a `TempoRange(doubleTempoIfBelow,
halveTempoIfAbove)` — only when *both* are set; a dance with just one threshold defined shows no
Range today (no real data exercises that case yet). `DanceType.validationRange` takes the broadest
range (`.include()`) across every instance that defines one, ignoring instances that don't — e.g.
Salsa's `"American Rhythm"` instance (no validation data) doesn't affect the dance-level Range,
only `"Social"` does.

The Range column's label and footnote intentionally frame it as "the broadest tempo range we
consider plausible for this dance style" rather than exposing the "double/halve" internals — the
raw thresholds are an implementation detail of tempo-detection-error correction, but the range they
imply is a reasonable thing for a visitor to see.

### Blog link

`DanceName.vue` gained an optional `showBlogLink` prop (default `false`, so every other caller
renders unchanged): when true and the dance's `blogTag` is set (`DanceObject.blogTag`, populated
from `dances.json`), it renders a small `IBiNewspaper` icon link right after the name, linking to
`https://music4dance.blog/tag/{blogTag}` with `title="Blog Posts"` as the tooltip/alt text — the
same URL pattern `dance-details/components/DanceContents.vue` already uses for its own blog link.
`blogTag` lives on `DanceObject` and `DanceGroup` independently (not on the common `NamedObject`
base both extend), so `blogLink` narrows via a cast (`(props.dance as { blogTag?: string }).blogTag`)
rather than importing and branching on both concrete types. `TempoList.vue` passes `show-blog-link`
unconditionally on its Name cell; dances without a `blogTag` simply render no icon.

### Column chooser

Below the `BTable`, `TempoList.vue` renders a second, small `CheckedList` (reusing the same
dropdown-with-checkboxes component the four page-level filters use, via `type="Column"`,
`variant="outline-secondary"`, `size="sm"` so it reads as a secondary/advanced control rather than
another primary filter) bound to a `visibleColumns` ref. Name is not offered as a choice — it's
`stickyColumn: true` and always rendered — but Meter, BPM, MPM, Type, Styles, and Range all are,
driven by a `chooseableColumns: { key, label, defaultVisible }[]` array local to the component. The
`fields` passed to `BTable` is a `computed` that filters the full field-definition list
(`allFields`) down to `name` plus whatever's in `visibleColumns`. Meter/BPM/MPM/Type/Styles are
`defaultVisible: true`; Range is `defaultVisible: false` (per `dances.json`'s "Known Gaps"-era
convention of adding new optional columns hidden, so the table's appearance doesn't change for
anyone who doesn't open the chooser). A `<p>` footnote below the chooser explains what Range means,
shown only when `visibleColumns` includes `"validationRange"`.

The selection can be seeded two ways, in addition to each column's own `defaultVisible`:

- `TempoList`'s optional `initialColumns?: string[]` prop, sourced from `App.vue`'s
  `model.columns` (see "Server-Side Wiring" above) — when given, `visibleColumns` is
  `filterValid(allColumnKeys, initialColumns)` instead of the default-visible set; an
  `initialColumns` containing only invalid keys yields an empty column set (Name still always
  shows), same "silently drop what's invalid" behavior the four page-level filters already use.
- Manual interaction with the chooser itself, which is plain component-local `ref` state — it
  resets on page reload (aside from whatever `initialColumns` re-seeds from the URL); there's no
  localStorage persistence.

`filterValid` (`m4d/ClientApp/src/models/CheckboxTypes.ts`) was promoted from an `App.vue`-local
function to a shared export specifically so `TempoList.vue` could reuse the same "seed from a
server/query value, drop anything invalid" logic for `initialColumns` — `App.vue`'s four filters
and this prop now share one implementation.

`CheckedList.vue` gained two optional props to support the column-chooser use, both defaulting to
the original behavior so the four page-level filters render unchanged: `variant` (`ButtonVariant`,
default `"primary"`) and `size` (`Size`, default unset/normal), both passed straight through to the
underlying `BDropdown`.

## Testing

- `m4d/ClientApp/src/pages/tempo-list/__tests__/App.test.ts` — mounts the real page (via
  `loadTestPage`, real `bootstrap-vue-next` components, real dance content JSON as test data — see
  [[testing-patterns]] "Client-Side Testing Patterns") and exercises the filter pipeline both
  programmatically (assigning to the exposed `styles`/`types`/`meters`/`organizations`/`nameFilter`
  refs) and through genuine DOM checkbox interaction (`input.setValue(true/false)` — `trigger("click")`
  does not reliably flip a `BFormCheckboxGroup` checkbox in jsdom, which is why the equivalent
  interaction test in `CheckedList.test.ts` was previously left `test.skip`). Includes regression
  tests for all four fixes below: the tempo-based exclusion (Performance dances *and* Pattern),
  the dropped "Performance" Type option, both the "select all organizations" case and a
  deliberate narrow organization selection (to prove the fix didn't broaden the latter), and the
  Type column narrowing to the selected group(s). Also covers the name filter, including that it
  ANDs with the other filters (a group match whose name doesn't satisfy the text filter is
  excluded).
- `m4d/ClientApp/src/pages/tempo-list/components/__tests__/TempoList.test.ts` — unit tests for the
  results table: column content/links for a known dance, the empty-selection caption, default sort
  order, and the column chooser (every optional column visible by default except Range, Name not
  offered as a choice, unchecking a column removes its header and cell content from the table).
  Also covers: Range hidden by default but showing the validation-derived range (blank for dances
  without one) once checked, the Range footnote appearing only once that column is visible,
  `initialColumns` seeding a custom set (and silently dropping unknown keys), and the blog-link icon
  appearing only for a dance with a `blogTag`.
- `m4d/ClientApp/src/pages/tempo-list/components/__tests__/CheckedList.test.ts` — pre-existing;
  covers the dropdown label logic, including real checkbox interaction (`setValue`, not
  `trigger("click")` — see "Fixed (2026-07-15)" below); also covers the `counts` prop in isolation
  (no prop → unannotated options, same markup as before; with it → `"(N)"` per option, zero-count
  options muted via the label's wrapping `<span>`, and a zero-count checkbox is still checkable, not
  `disabled`).
- `m4d/ClientApp/src/models/DanceDatabase/__tests__/DanceDatabaseFiltering.test.ts` and
  `DanceDatabase.test.ts` — unaffected by the fixes above (re-verified): the `matchOrganizations`
  fix lives in `App.vue`, not `DanceFilter`, specifically so these fixtures' narrow,
  single-organization selections keep excluding organization-less dances as before; the
  `DanceDatabase.filter()` groups fix only changes which *groups* come back, not which *dances* do;
  and none of those fixtures pass a `groups` criterion, so the `.groups`-narrowing fix doesn't
  touch them either.
- `m4d/ClientApp/src/models/DanceDatabase/__tests__/DanceFilter.test.ts` — unit-level coverage for
  the `.groups`-narrowing fix directly: narrows to the selected group(s), doesn't mutate the
  original dance's `.groups` array, and leaves `.groups` untouched when no group filter is set.
- `App.test.ts` also covers the counts feature end-to-end: per-option counts reflect the *other*
  three facets' current selection independent of the facet's own selection (narrowing Type to
  Waltz zeroes American Rhythm's style count without American Rhythm itself being checked), the
  default (everything selected) view gives every option a nonzero count, organization counts use
  the strict single-organization count rather than the "select all" normalization (verified against
  the exact 8-dance UCWDC count from the "filters by organization" test), and a real-DOM check that
  the Style dropdown visibly grays out a zero-count option once Type is narrowed. A further test
  seeds `model.columns` and checks the rendered `<thead>` reflects it end-to-end through
  `TempoList`'s `initial-columns` prop.
- `m4d/ClientApp/src/models/DanceDatabase/__tests__/DanceInstance.test.ts` and `DanceType.test.ts` —
  cover `validationRange`: undefined with no validation data, undefined when only one of the two
  thresholds is set, the instance-level range spanning `doubleTempoIfBelow`–`halveTempoIfAbove`, and
  the dance-level aggregate taking the broadest range across only the instances that define one
  (mirroring Salsa's real "Social"-only data).
- `m4d/ClientApp/src/components/__tests__/DanceName.test.ts` — covers `blogLink` (derived from
  `blogTag`, undefined without one) and `showBlogLink` (no icon without a `blogTag` even when true,
  no icon with a `blogTag` but the prop left at its `false` default, icon + correct `href`/`title`
  when both are present).

## Known Gaps / Follow-ups

- `App.vue:11` has a standing `TODO` to clean up the `CheckboxOptions` structures generally
  (unrelated to filtering behavior — the "disable checkboxes with no results" half of that TODO is
  resolved, see "Checkbox cross-filtering" above and "Fixed (2026-07-15)" below).

## Added (2026-07-15 follow-up 2)

Surfaced three pieces of `dances.json` data this page wasn't showing yet:

1. A hidden-by-default **Range** column, backed by the new `DanceValidation` model and
   `DanceInstance`/`DanceType.validationRange` getters — see "Range column / `DanceValidation`"
   above. Currently only populated for Salsa.
2. A `?columns=` query-string parameter (`HomeController.Tempi`, `TempoListModel.Columns`,
   `App.vue`'s `model.columns` → `TempoList`'s `initial-columns` prop) so a custom column set can be
   linked to directly — see "Column chooser" above. `CheckboxTypes.ts`'s `filterValid` was promoted
   from an `App.vue`-local function to a shared export to back this.
3. A "Blog Posts" icon link (`DanceName.vue`'s new `showBlogLink` prop) next to each dance's name,
   linking to that dance's `blogTag` on `https://music4dance.blog` — see "Blog link" above.

## Fixed (2026-07-15)

1. The Meter dropdown now reads `model.meters` on load, via `filterValidMeters()`
   (`App.vue`) — see the "Filter state" section above. Previously `meters` was always initialized
   to all three hard-coded options regardless of the `?meters=` query-string parameter, even though
   the server-side `TempoListModel.Meters` was populated and available on `model_`.
2. `CheckedList.test.ts`'s interaction test (`"handle checking a single item"`) is un-skipped.
   Two independent bugs in the test, not `CheckedList.vue`, were masking it:
   - `trigger("click")` doesn't reliably flip a `BFormCheckboxGroup` checkbox in jsdom;
     `setValue(true)` does (same fix already used by `App.test.ts`'s real-interaction tests).
   - The test's mock update handler was keyed `"onUpdate:model-value"` (hyphenated), which never
     matches Vue's emitted `update:modelValue` event — the model prop was never actually being
     written back, independent of the click/jsdom issue.
   - Separately, `.attributes("checked")` never reflects a checkbox's true state in Vue 3: Vue
     patches `checked` on an `<input>` as a DOM *property* (`element.checked`), not an HTML
     attribute, so an assertion against the attribute is always falsy regardless of actual state.
     The two `toMatchSnapshot` tests in the same file had a stale snapshot (unrelated to the above —
     just a `bootstrap-vue-next` markup drift since the snapshot was last recorded) and one of the
     same `.attributes("checked")` assertions; both are fixed and re-enabled alongside the
     interaction test.
3. Added the per-option result counts / cross-filtering feature — see "Result counts /
   cross-filtering" above. Resolves the `App.vue` TODO's "consider disabling checkboxes that can't
   produce any results" in favor of the more common faceted-search pattern (count + gray-out,
   still checkable) rather than actually disabling options.

## Fixed (2026-07-14)

Three bugs found while first documenting this page (see git history for this file/commit) were
fixed together, since the first two were both in `DanceFilter`/`DanceDatabase`, shared with other
callers:

1. Dances with no real tempo (Performance dances, and Pattern) are now excluded by
   `tempoRange.isInfinite` rather than by "Performance" group membership — see the data-flow
   section above.
2. `DanceDatabase.filter()`'s `.groups` getter now derives from the dances it just filtered,
   not the pre-filter list, so a filtered-out group (like "Performance," before fix #1 subsumed
   it) no longer lingers as a dead dropdown option.
3. `App.vue`'s `dances` computed normalizes "every organization checkbox checked" to
   `organizations: undefined` before building its `DanceFilter`, so the default view no longer
   silently hides every organization-less "Social" dance. `DanceFilter.matchOrganizations` itself
   was deliberately left unchanged (a narrow, specific organization selection should still exclude
   unaffiliated dances).

A fourth bug, found separately afterward: `DanceFilter.reduce()`'s `.groups` narrowing (see
"TempoList.vue" above) — the Type column didn't shrink to the selected group(s) the way
BPM/MPM/Styles already shrank to the selected style(s).

## Related Code

| File | Purpose |
| --- | --- |
| `m4d/ClientApp/src/pages/tempo-list/App.vue` | Page: builds filter option lists, holds selection state (including `nameFilter`), computes `dances` |
| `m4d/ClientApp/src/pages/tempo-list/components/CheckedList.vue` | Reusable multi-select dropdown used for the four page-level filters and (via `variant`/`size`) the column chooser |
| `m4d/ClientApp/src/pages/tempo-list/components/TempoList.vue` | Results table; owns the column chooser and `visibleColumns` state |
| `m4d/ClientApp/src/components/NameFilterInput.vue` | Shared name-filter text input (search icon + `BInputGroup`), used here and by `dance-index`'s `DanceTable.vue` |
| `m4d/ClientApp/src/models/DanceDatabase/DanceFilter.ts` | Filter matching logic shared with other dance-filtering pages |
| `m4d/ClientApp/src/models/DanceDatabase/DanceDatabase.ts` | Dance/group/style/organization aggregation, `.filter()`, `.filterByName()` (name-filter matching, shared across pages) |
| `m4d/ClientApp/src/models/DanceDatabase/DanceValidation.ts` | `doubleTempoIfBelow`/`halveTempoIfAbove` thresholds; mirrors `DanceLib/DanceValidation.cs` |
| `m4d/ClientApp/src/models/CheckboxTypes.ts` | `CheckboxOption`/value conversion helpers (`optionsFromText`, `valuesFromOptions`, `textFromValues`, `filterValid`) |
| `m4d/Controllers/HomeController.cs` | `Tempi` action |
| `m4d/ViewModels/TempoListModel.cs` | Server-side model matching the client `TempoListModel` interface |
| `m4d/Views/Shared/Vue3.cshtml`, `_environmentWriter.cshtml` | Generic Vue3 page host; emits `model_` and `window.danceDatabaseJson` |
