# Tempo Counter Page (`tempo-counter/App.vue`)

**Type:** Reference
**Status:** Current
**Last verified:** 2026-11-19 (pitch badge added; other behavior not re-traced)
**Code:** `m4d/ClientApp/src/pages/tempo-counter/`, `m4d/Controllers/HomeController.cs` (`Counter`)

## Overview

The Tempo Counter page (`m4d/ClientApp/src/pages/tempo-counter/App.vue`) is a reference tool at
`/Home/Counter` ("Counter" in the site map) that lets a visitor tap out a beat (or type in a known
tempo) and see which dances match, ranked by how close their tempo range is to the tapped value.
Like the [Tempo List page](tempo-list-page.md), it does no server-side filtering — the full dance
database ships to the client as `window.danceDatabaseJson`, and every click/input recomputes the
match list against `DanceDatabase.filterTempo`.

The page is a common blog deep-link target (`blogmap.txt` links to both `/Home/counter` and
`/Home/tempi`), which is the motivating use case for its `numerator`/`tempo`/`count`/`epsilon` query
parameters: a blog post (or a visitor's own bookmark/copied link — see "Shareable URLs" below) can
link straight into a specific starting tempo/meter/count-mode/strictness instead of a blank
counter.

## Server-Side Wiring

- `HomeController.Counter(numerator, tempo, count, epsilon, show)`
  (`m4d/Controllers/HomeController.cs`) reads four scalar query-string parameters (`int?
  numerator`, `decimal? tempo`, `string count = "beats"`, `decimal? epsilon`) and one multi-value
  one (`List<string> show`, see "Which details to show" below). It renders the generic Vue3 host
  view with:
  - component name `"tempo-counter"` (resolves to this `App.vue`)
  - a `TempoCounterModel` (`m4d/ViewModels/TempoCounterModel.cs`) built directly from the
    parameters, with `show` going through `ConvertParameter` (empty → `null`) as `Tempi`'s lists
    do
  - `danceEnvironment: true`, which emits `window.danceDatabaseJson` the same way `Tempi` does
- No `[Route]` attribute; the URL is the default MVC route,
  `/Home/Counter?numerator=4&tempo=120&count=beats&epsilon=5`.
- All four parameters are passed through to `model_` untyped/unvalidated server-side — fencing is
  entirely the client's job, via `QueryValidation.ts` (see below). This mirrors Tempo List's
  `filterValid`/`filterValidMeters` convention of trusting the server to pass along whatever it was
  given and having the client silently fall back to a sane default for anything malformed, rather
  than the server rejecting or normalizing the request.

## Client-Side Data Flow

```text
window.danceDatabaseJson ──▶ safeDanceDatabase() ──▶ danceDatabase: DanceDatabase
                                                          │
                                              dances = danceDatabase.dances   // unfiltered — no
                                                                              // style/type/org
                                                                              // narrowing here
model_.numerator ──▶ validateNumerator ──▶ beatsPerMeasure (ref, default 4)
model_.tempo      ──▶ validateTempo      ──▶ beatsPerMinute (ref, default 0)
model_.count      ──▶ validateCountMethod ──▶ countMethod (ref, default "beats")
model_.epsilon    ──▶ validateEpsilon    ──▶ epsilonPercent (ref, default 5)
                                                          │
                        tempoType = countMethod === "measures" ? Measures : Beats
                        measuresPerMinute = computed get/set bridging beatsPerMinute
                                            via beatsPerMeasure (writable computed, not
                                            a separate ref — writes flow back into
                                            beatsPerMinute)
                                                          │
                        ┌─────────────────────────────────┴─────────────────────────────────┐
                        ▼                                                                     ▼
              <TempoCounter v-model:*>                                          <DanceDeltas :dances
              (tap/typed-tempo input)                                             :beats-per-minute
                                                                                    :beats-per-measure
                                                                                    :tempo-type
                                                                                    :epsilon-percent
                                                                                    @choose-dance>
```

Like `model_` on the Tempo List page, `model_` here is the *object itself* (not a JSON string) —
`App.vue` reads `model_.numerator`/`.tempo`/`.count` directly rather than calling
`TypedJSON.parse`.

All four query-string-seeded values (`beatsPerMeasure`, `beatsPerMinute`, `countMethod`,
`epsilonPercent`) round-trip through `model_`, each fenced by its own validator in
`m4d/ClientApp/src/pages/tempo-counter/QueryValidation.ts`:

| Validator | Accepts | Falls back to |
| --- | --- | --- |
| `validateNumerator` | `2`, `3`, or `4` — the only meters `MeasuresPerMinute.vue`'s dropdown offers | `DEFAULT_NUMERATOR = 4` |
| `validateTempo` | a finite number in `[0, 500]`, matching `TempoModal.vue`'s manual-entry bounds | `DEFAULT_TEMPO = 0` |
| `validateCountMethod` | literally `"measures"` (anything else, including garbage) | `DEFAULT_COUNT_METHOD = "beats"` |
| `validateEpsilon` | a finite number in `[0, 20]`, matching `StrictSlider.vue`'s range bounds | `DEFAULT_EPSILON = 5` |

Each validator silently drops anything else (missing, wrong type, out of range) rather than
throwing or trusting it through — the same "silently drop invalid seeded value" convention
`tempo-list`'s `filterValid`/`filterValidMeters` already use, just one field at a time instead of
against an options list.

Unlike the Tempo List page, `dances` here is **not** filtered to `!tempoRange.isInfinite` up front
— Performance-group dances and Pattern would only be excluded downstream if their tempo delta
happens to fall outside `epsilonPercent`, not deliberately.

## `TempoCounter.vue` (input controls)

Owns the tap-tempo state machine and the manual-entry widgets. All state is passed in/out via
`defineModel` (`beatsPerMeasure`, `beatsPerMinute`, `measuresPerMinute`, `countMethod`,
`epsilonPercent`), so `App.vue` is the single owner of the actual ref values.

- **Tap-tempo state machine** (`ClickState`: `Initial → FirstClick → Counting → Done`, `maxWait =
  5000`ms): each click records the delta from the previous click into a rolling window of up to 10
  intervals (`intervals.value`); a `watchEffect` averages them into a beats/measures-per-minute
  rate and writes `beatsPerMinute`. If the state is `Done` with an **empty** interval list — i.e. a
  tempo arrived via `measuresPerMinute`/the query string rather than tapping — the same
  `watchEffect` instead derives `beatsPerMinute` from `measuresPerMinute * beatsPerMeasure` so a
  seeded/typed value isn't clobbered back to `0`.
- Switching `countMethod` (Beats/Measures button group) calls `timerReset()`, discarding any
  in-progress tap sequence rather than reinterpreting it under the new mode.
- **`BeatsPerMinute.vue`** / **`MeasuresPerMinute.vue`** — read-only display buttons that open a
  shared **`TempoModal.vue`** (a small numeric-entry modal) to type an explicit value directly,
  bypassing tapping entirely. `MeasuresPerMinute.vue` additionally exposes a `BDropdown` to set
  `beatsPerMeasure` directly to 2, 3, or 4 (the only meters `dances.json` uses) — this is the
  client-side equivalent of the Tempo List page's hard-coded `[2/4, 3/4, 4/4]` meter options.
- **`StrictSlider.vue`** — the `epsilonPercent` control, a plain `0`–`20` step-`0.5` range input
  labeled "less"/"more" rather than showing the raw percentage.

## `DanceDeltas.vue` / `TempoDeltaInfo.vue` (results list)

`DanceDeltas.vue` narrows and ranks `props.dances` in two steps:

1. **Meter pre-filter** — `beatsPerMeasure` of `2` or `4` both use one `DanceFilter({ meters: [2/4,
   4/4] })` (`duplefilter`); `3` uses its own `DanceFilter({ meters: [3/4] })` (`triplefilter`).
   Any other numerator (unreachable through this page's own UI, since only 2/3/4 are offered, but
   still reachable via `?numerator=` in the URL) applies **no** meter filter at all — every dance
   is tempo-compared regardless of meter.
2. **Tempo ranking** — `DanceDatabase.filterTempo(dances, beatsPerMinute, epsilonPercent)`
   (`DanceDatabase.ts:131-136`) maps every remaining dance to a `DanceOrder` (via
   `TempoRange.calculateDelta`/`calculateDeltaPercent`), drops any whose
   `deltaPercentAbsolute > epsilonPercent`, and sorts ascending by `deltaPercentAbsolute` — so the
   closest tempo match is always first, and the strictness slider is a live inclusion threshold,
   not just a display cutoff. The comparison is inclusive so that the slider's left end (`0`)
   means "only dances whose range contains the tempo"; with a strict `<` it emptied the list.

`TempoDeltaInfo.vue` renders each `DanceOrder` as a `BListGroupItem`:

- Variant is `"primary"` when the tempo is in the dance's range, meaning the difference in the
  displayed unit (BPM or MPM, per `tempoType`) rounds to `0.0`. Otherwise it is `"warning"` (the
  song is slower than the dance: negative delta) or `"success"` (the song is faster). This used to
  be a fixed `abs(deltaMpm) < 1.0` threshold. That depended on each dance's meter, so at 93 BPM a
  2/4 Samba 3 BPM out (1.5 MPM) was highlighted while a 4/4 Bolero with the same range wasn't
  (0.75 MPM).
- `DanceName` is rendered with `show-meter`, so each range carries its meter (`48-52 MPM (2/4)`).
  The duple pre-filter mixes 2/4 and 4/4 dances, and each range is in the dance's _own_ measures,
  so without the meter a 2/4 dance reads as twice the tempo of the counter's 4/4 MPM.
- Up to three badges (same variant), chosen by the `metrics` prop. Keys and defaults are in
  `pages/tempo-counter/deltaMetrics.ts`. `DanceDeltas` defaults `metrics` to `["tempo"]`, so
  `DanceChooser`'s "By Tempo" tab shows only the tempo badge. This page passes the visitor's
  selection from the **Show:** chooser (see "Which details to show" below). For 140 BPM against
  Cha Cha's 128 BPM max, the badges are:
  - **tempo**: `"3.0 MPM fast"` / `"12.0 BPM fast"`. Units follow `tempoType` (Beats vs.
    Measures), matching whichever mode `countMethod` is currently in.
  - **pitch**: `"Pitch −8.6%"`, the CD player or DJ deck pitch-fader setting that brings the song
    to the dance's nearest range edge. It comes from `DanceOrder.pitchPercent` via
    `TempoRange.calculatePitchPercent` = `(edge − tempo) / tempo × 100`. This is deliberately
    relative to the *song's* tempo, since that is what a pitch fader scales. `deltaPercent` is
    relative to the range edge, so it can't be reused here: for the same example it gives 9.4%.
    The value isn't capped to a typical fader range (±8%/±16%), since modern decks and software go
    much further and the number is useful either way.
  - **speed**: `"Speed 91.4%"`, the same adjustment as a playback rate where 100% = unchanged
    (`DanceOrder.speedPercent` = `100 + pitchPercent`). This is how tempo-change software and sites
    such as pitch-tempo.online express it (target ÷ current × 100).
- In-range rows render no badges at all. The wrapper uses `v-if`, not `v-show`: its Bootstrap
  `d-flex` class is `display: flex !important`, which overrides `v-show`'s inline `display: none`.
- Clicking a row emits `choose-dance(danceId, ctrlKey)`; `App.vue`'s `chooseDance(danceId)` handler
  only declares one parameter, so the `ctrlKey` argument is silently dropped — there's currently no
  distinction between a plain click and a ctrl/cmd-click (both `window.open(..., "_blank")` to
  `/dances/{seoName}`), unlike a native link where ctrl-click is a browser-native "open in
  background tab" gesture. `DanceName`'s own link is suppressed via `hide-name-link="true"` since
  the whole list item is already clickable.

## Which details to show (`?show=`)

A **Show:** `CheckedList` (the Tempo List page's column-chooser component) sits on the same row as
**Copy Link to This Tempo**. It selects which of the three badges above each near-miss dance
gets. The selection comes from `?show=` (repeatable, e.g. `?show=tempo&show=speed`):
`HomeController.Counter` passes it through as `TempoCounterModel.Show`, and `validateDeltaMetrics`
fences it on the client. That fencing follows Tempo List's `?columns=` convention: unknown keys
are dropped, a missing param falls back to the default (`tempo` only), and badges always
render in the fixed tempo/pitch/speed order regardless of the order given. A short note under the
list explains Pitch and Speed whenever either one is shown, since hover titles don't help on touch
devices.

## Shareable URLs

Like the Tempo List page, the address bar stays continuously in sync with the counter's current
configuration via the shared `useUrlQuerySync` composable, and a **Copy Link to This Tempo** button
(`CopyLinkButton`, defaulting to `window.location.href`) sits below the counter controls. See
[tempo-list-page](tempo-list-page.md) "Shareable URLs" for how the shared mechanism works generally; this page's
wiring (`App.vue`) is:

```ts
useUrlQuerySync(() => ({
  numerator: beatsPerMeasure.value.toString(),
  tempo: beatsPerMinute.value.toFixed(1),
  count: countMethod.value,
  epsilon: epsilonPercent.value.toString(),
  show: arraysEqualAsSets(metrics.value, defaultDeltaMetrics) ? undefined : metrics.value,
}));
```

`tempo` is rounded to one decimal place (`toFixed(1)`) rather than the raw float — the tap-tempo
state machine and the `measuresPerMinute`/`beatsPerMinute` bridging computed can both produce many
more significant digits than `BeatsPerMinute.vue`/`MeasuresPerMinute.vue` ever actually display, so
the shared link matches what the visitor sees rather than exposing that extra, meaningless
precision. Unlike Tempo List's filters, none of the four tempo params has a default worth omitting,
because every counter configuration is equally likely to be the one worth sharing. Those four are
always in the URL. `show` is the exception: like Tempo List's `columns`, it's omitted at its
default set.

## Testing

`m4d/ClientApp/src/pages/tempo-counter/__tests__/QueryValidation.test.ts` and `App.test.ts` cover
the validation/fencing behavior above (each validator's accept/reject boundary), the `model_`
seeding/round-trip for all four values, the tap-tempo state machine, the
`measuresPerMinute`/`beatsPerMinute` bridging computed, and the shareable-URL sync (initial seed,
live updates, and the one-decimal rounding on `tempo`). The meter pre-filter/ranking logic in
`DanceDeltas.vue` is still unverified by automated tests — a gap relative to the rest of this page,
which now otherwise matches the Tempo List page's coverage level (see [tempo-list-page](tempo-list-page.md)
"Testing").

## Known Gaps / Follow-ups

- The `ctrlKey` argument on `choose-dance` is accepted by `DanceDeltas`/`TempoDeltaInfo` but
  ignored by `App.vue`'s handler — dead plumbing, or an unfinished "open in same tab vs. new tab"
  feature, depending on intent.
- `DanceDeltas.vue`'s meter pre-filter/ranking logic has no dedicated test coverage (see "Testing"
  above).

## Related Code

| File | Purpose |
| --- | --- |
| `m4d/ClientApp/src/pages/tempo-counter/App.vue` | Page: seeds state from `model_`, holds `beatsPerMeasure`/`beatsPerMinute`/`countMethod`/`epsilonPercent`, wires the counter to the results list |
| `m4d/ClientApp/src/pages/tempo-counter/CountMethod.ts` | `CountMethod` union type (`"measures" \| "beats"`) |
| `m4d/ClientApp/src/pages/tempo-counter/QueryValidation.ts` | Per-field validation/fencing + defaults for all four query-string-seeded values |
| `m4d/ClientApp/src/composables/useUrlQuerySync.ts` | Shared live-URL-sync composable (see [tempo-list-page](tempo-list-page.md) "Shareable URLs") |
| `m4d/ClientApp/src/components/CopyLinkButton.vue` | Shared "Copy Link" button (see [tempo-list-page](tempo-list-page.md) "Shareable URLs") |
| `m4d/ClientApp/src/pages/tempo-counter/components/TempoCounter.vue` | Tap-tempo state machine + manual-entry/meter/strictness controls |
| `m4d/ClientApp/src/pages/tempo-counter/components/BeatsPerMinute.vue` | BPM display button + manual-entry trigger |
| `m4d/ClientApp/src/pages/tempo-counter/components/MeasuresPerMinute.vue` | MPM display button + manual-entry trigger + meter (2/3/4) dropdown |
| `m4d/ClientApp/src/pages/tempo-counter/components/TempoModal.vue` | Shared numeric-entry modal used by both BPM and MPM manual-entry buttons |
| `m4d/ClientApp/src/pages/tempo-counter/components/StrictSlider.vue` | `epsilonPercent` range slider |
| `m4d/ClientApp/src/pages/tempo-counter/components/DanceDeltas.vue` | Meter pre-filter + `DanceDatabase.filterTempo` ranking |
| `m4d/ClientApp/src/pages/tempo-counter/components/TempoDeltaInfo.vue` | Single result row: variant and the tempo/pitch/speed badges derived from the tempo delta |
| `m4d/ClientApp/src/pages/tempo-counter/deltaMetrics.ts` | The three badge keys, their default set, and `validateDeltaMetrics` for `?show=` |
| `m4d/ClientApp/src/models/DanceDatabase/DanceOrder.ts` | Wraps a `DanceType` with `delta`/`deltaPercent`/`deltaMpm`/`pitchPercent`/`speedPercent` relative to a tapped tempo |
| `m4d/ClientApp/src/models/DanceDatabase/DanceDatabase.ts` | `filterTempo()` — maps, thresholds, and sorts by tempo delta (shared with other tempo-matching UI) |
| `m4d/ClientApp/src/models/DanceDatabase/DanceFilter.ts` | Filter matching logic; used here only for the meter pre-filter |
| `m4d/Controllers/HomeController.cs` | `Counter` action |
| `m4d/ViewModels/TempoCounterModel.cs` | Server-side model matching the client `TempoModel` interface |
| `m4d/Views/Shared/Vue3.cshtml`, `_environmentWriter.cshtml` | Generic Vue3 page host; emits `model_` and `window.danceDatabaseJson` (see [tempo-list-page](tempo-list-page.md) "Server-Side Wiring") |
