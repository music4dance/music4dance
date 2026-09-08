# Tempo Counter Page (`tempo-counter/App.vue`)

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

- `HomeController.Counter(numerator, tempo, count, epsilon)` (`m4d/Controllers/HomeController.cs`)
  reads four scalar query-string parameters — `int? numerator`, `decimal? tempo`,
  `string count = "beats"`, `decimal? epsilon` — and renders the generic Vue3 host view with:
  - component name `"tempo-counter"` (resolves to this `App.vue`)
  - a `TempoCounterModel` (`m4d/ViewModels/TempoCounterModel.cs`) built directly from the four
    parameters (no `ConvertParameter`/list handling — unlike `Tempi`, none of these are
    multi-value)
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
   `deltaPercentAbsolute >= epsilonPercent`, and sorts ascending by `deltaPercentAbsolute` — so the
   closest tempo match is always first, and the strictness slider is a live inclusion threshold,
   not just a display cutoff.

`TempoDeltaInfo.vue` renders each `DanceOrder` as a `BListGroupItem`:

- Variant is `"primary"` when `abs(deltaMpm) < 1.0` (close enough that no direction is worth
  flagging), else `"warning"` (dance's tempo is slower than tapped — negative delta) or `"success"`
  (dance is faster).
- A `BBadge` (same variant) shows e.g. `"2.3 BPM fast"`/`"1.1 MPM slow"` — units follow
  `tempoType` (Beats vs. Measures), matching whichever mode `countMethod` is currently in.
- Clicking a row emits `choose-dance(danceId, ctrlKey)`; `App.vue`'s `chooseDance(danceId)` handler
  only declares one parameter, so the `ctrlKey` argument is silently dropped — there's currently no
  distinction between a plain click and a ctrl/cmd-click (both `window.open(..., "_blank")` to
  `/dances/{seoName}`), unlike a native link where ctrl-click is a browser-native "open in
  background tab" gesture. `DanceName`'s own link is suppressed via `hide-name-link="true"` since
  the whole list item is already clickable.

## Shareable URLs

Like the Tempo List page, the address bar stays continuously in sync with the counter's current
configuration via the shared `useUrlQuerySync` composable, and a **Copy Link to This Tempo** button
(`CopyLinkButton`, defaulting to `window.location.href`) sits below the counter controls. See
[[tempo-list-page]] "Shareable URLs" for how the shared mechanism works generally; this page's
wiring (`App.vue`) is:

```ts
useUrlQuerySync(() => ({
  numerator: beatsPerMeasure.value.toString(),
  tempo: beatsPerMinute.value.toFixed(1),
  count: countMethod.value,
  epsilon: epsilonPercent.value.toString(),
}));
```

`tempo` is rounded to one decimal place (`toFixed(1)`) rather than the raw float — the tap-tempo
state machine and the `measuresPerMinute`/`beatsPerMinute` bridging computed can both produce many
more significant digits than `BeatsPerMinute.vue`/`MeasuresPerMinute.vue` ever actually display, so
the shared link matches what the visitor sees rather than exposing that extra, meaningless
precision. Unlike Tempo List, none of these four params have a "default" worth omitting — every
counter configuration is equally likely to be the one worth sharing — so all four are always
present in the URL.

## Testing

`m4d/ClientApp/src/pages/tempo-counter/__tests__/QueryValidation.test.ts` and `App.test.ts` cover
the validation/fencing behavior above (each validator's accept/reject boundary), the `model_`
seeding/round-trip for all four values, the tap-tempo state machine, the
`measuresPerMinute`/`beatsPerMinute` bridging computed, and the shareable-URL sync (initial seed,
live updates, and the one-decimal rounding on `tempo`). The meter pre-filter/ranking logic in
`DanceDeltas.vue` is still unverified by automated tests — a gap relative to the rest of this page,
which now otherwise matches the Tempo List page's coverage level (see [[tempo-list-page]]
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
| `m4d/ClientApp/src/composables/useUrlQuerySync.ts` | Shared live-URL-sync composable (see [[tempo-list-page]] "Shareable URLs") |
| `m4d/ClientApp/src/components/CopyLinkButton.vue` | Shared "Copy Link" button (see [[tempo-list-page]] "Shareable URLs") |
| `m4d/ClientApp/src/pages/tempo-counter/components/TempoCounter.vue` | Tap-tempo state machine + manual-entry/meter/strictness controls |
| `m4d/ClientApp/src/pages/tempo-counter/components/BeatsPerMinute.vue` | BPM display button + manual-entry trigger |
| `m4d/ClientApp/src/pages/tempo-counter/components/MeasuresPerMinute.vue` | MPM display button + manual-entry trigger + meter (2/3/4) dropdown |
| `m4d/ClientApp/src/pages/tempo-counter/components/TempoModal.vue` | Shared numeric-entry modal used by both BPM and MPM manual-entry buttons |
| `m4d/ClientApp/src/pages/tempo-counter/components/StrictSlider.vue` | `epsilonPercent` range slider |
| `m4d/ClientApp/src/pages/tempo-counter/components/DanceDeltas.vue` | Meter pre-filter + `DanceDatabase.filterTempo` ranking |
| `m4d/ClientApp/src/pages/tempo-counter/components/TempoDeltaInfo.vue` | Single result row: variant/badge derived from tempo delta |
| `m4d/ClientApp/src/models/DanceDatabase/DanceOrder.ts` | Wraps a `DanceType` with `delta`/`deltaPercent`/`deltaMpm` relative to a tapped tempo |
| `m4d/ClientApp/src/models/DanceDatabase/DanceDatabase.ts` | `filterTempo()` — maps, thresholds, and sorts by tempo delta (shared with other tempo-matching UI) |
| `m4d/ClientApp/src/models/DanceDatabase/DanceFilter.ts` | Filter matching logic; used here only for the meter pre-filter |
| `m4d/Controllers/HomeController.cs` | `Counter` action |
| `m4d/ViewModels/TempoCounterModel.cs` | Server-side model matching the client `TempoModel` interface |
| `m4d/Views/Shared/Vue3.cshtml`, `_environmentWriter.cshtml` | Generic Vue3 page host; emits `model_` and `window.danceDatabaseJson` (see [[tempo-list-page]] "Server-Side Wiring") |
