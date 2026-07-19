# Waltz 4/4 Correction Controls

## Problem

A significant number of songs are tagged as both a Waltz dance style (SWZ, CSW, VWZ, TGV) and the meter tag `4/4:Tempo`. Waltzes are by definition 3/4, so this combination is almost always a data error introduced by the automated tempo algorithm. This feature gives `canEdit` users a one-click-then-save flow to correct these songs directly from the song details page.

---

## Detection Conditions

The correction card is shown only when **all** of the following are true:

1. The user is authenticated and has the `canEdit` role (`context.canEdit`).
2. The song has at least one positive waltz dance rating — weight > 0 for any dance in the **WLZ** dance group.
3. The song has a `4/4:Tempo` tag (`song.hasMeterTag(4)` returns `true`).
4. The page is **not** currently in edit mode (hide the card once any correction has been queued).

The waltz dance IDs are derived at runtime from the **WLZ group** in the dance database (`dancegroups.json`), currently `["CSW", "SWZ", "VWZ", "TGV"]`. Detection filters `song.danceRatings` for any entry whose `danceId` is in that set and whose `weight > 0`.

```typescript
const WALTZ_IDS: string[] =
  safeDanceDatabase().groups.find((g) => g.id === "WLZ")?.danceIds ?? [];
```

This means adding or removing a waltz from the WLZ group in `dancegroups.json` automatically updates detection — no code change needed.

---

## Correction Actions

### Case 1 — "It's Fake" (4/4 is intentional)

> Rare: an expert performer genuinely dances this waltz to a 4/4 track.

**Mutations:**

- For each waltz dance rating on the song, add a dance-specific `Fake:Tempo` tag:

  ```txt
  Tag+:SWZ = Fake:Tempo      (if SWZ is present)
  Tag+:VWZ = Fake:Tempo      (if VWZ is present)
  Tag+:CSW = Fake:Tempo      (if CSW is present)
  Tag+:TGV = Fake:Tempo      (if TGV is present)
  ```

  Using `editor.addProperty(\`${PropertyType.addedTags}:${danceId}\`, "Fake:Tempo")` for each waltz.

- The `4/4:Tempo` song-level tag is **left in place** — the 4/4 is accurate for this track.

**Result after save:** The dance entry gains a `Fake:Tempo` chip. Future queries can filter or flag these with `Fake:Tempo` to separate them from real waltz tempo matches.

---

### Case 2 — "Bad 4/4 tag" (song is a waltz, tempo value is correct)

> The song tempo is correct in BPM, but the 4/4 meter tag was added incorrectly — the algorithm measured BPM accurately but misidentified the meter.

**Mutations:**

- Remove `4/4:Tempo` song-level tag:
  ```
  Tag- = 4/4:Tempo
  ```
- Add `3/4:Tempo` song-level tag only if it does not already exist:
  ```
  Tag+ = 3/4:Tempo   (skip if song.hasMeterTag(3))
  ```

Using:

```typescript
editor.addProperty(PropertyType.removedTags, "4/4:Tempo");
if (!song.hasMeterTag(3)) {
  editor.addProperty(PropertyType.addedTags, "3/4:Tempo");
}
```

**Result after save:** Song meter tag corrected; BPM unchanged.

---

### Case 3 — "Bad 4/4 tag + bad tempo" (meter and tempo both wrong)

> A mis-tracked beat doesn't just corrupt the meter tag — it corrupts the BPM too, and it can be wrong in more than one way depending on what the algorithm actually locked onto (quarter notes, half notes, a tripled or thirded pulse, etc). Manually counting the true beat and comparing against each candidate correction is the standard workflow; this case surfaces every known ratio at once so that comparison happens on-screen instead of on a calculator.

**Mutations:**
Same meter-tag change as Case 2 (remove `4/4:Tempo`, add `3/4:Tempo` if absent) in every sub-case, plus a tempo change whose ratio depends on which sub-case is selected:

```txt
Tempo = round(song.tempo × numerator / denominator)
```

**Correction ratios (extensible table):**

| id       | What the algorithm likely did                        | Ratio (new/old) |
| -------- | ---------------------------------------------------- | --------------- |
| `4-to-3` | Counted 4 beats/measure (quarter notes) instead of 3 | 3 / 4           |
| `2-to-3` | Counted 2 beats/measure (half notes) instead of 3    | 3 / 2           |
| `div-3`  | Tripled the true tempo                               | 1 / 3           |
| `mul-3`  | Reported one-third of the true tempo                 | 3 / 1           |
| `double` | Reported half the true tempo                         | 2 / 1           |
| `halve`  | Reported double the true tempo                       | 1 / 2           |

This table is a plain data array (`TEMPO_CORRECTION_CASES`, see component section below), not one-off code per case. Adding a newly-observed failure mode (e.g. a compound-meter miscount) is a one-line entry — no template or handler changes required.

**Example:** A song at 120 BPM tagged 4/4 shows all six candidates at once: 90 BPM (`4-to-3`), 180 BPM (`2-to-3`), 40 BPM (`div-3`), 360 BPM (`mul-3`), 240 BPM (`double`), 60 BPM (`halve`). The user picks whichever matches the tempo they counted by hand.

**Result after save:** The meter tag is corrected (as in Case 2) and the BPM is set to the corrected value for whichever ratio was applied.

---

### Case 4 — "Compound Time" (song works for both 4/4 dances and waltz)

> Some songs have a slow 4/4 beat that works well for Foxtrot or similar dances, but also have an underlying triple-time feel that can be danced as a fast waltz (e.g., Viennese Waltz). The 4/4 beat is real and should stay — but the waltz needs to be identified as compound time.

**Mutations:**

- Add `12/8:Tempo` song-level tag (only if not already present):
  ```
  Tag+ = 12/8:Tempo
  ```
- Add `Compound Time:Tempo` dance-specific tag to each waltz dance rating:
  ```
  Tag+:SWZ = Compound Time:Tempo      (if SWZ is present)
  Tag+:VWZ = Compound Time:Tempo      (if VWZ is present)
  Tag+:CSW = Compound Time:Tempo      (if CSW is present)
  Tag+:TGV = Compound Time:Tempo      (if TGV is present)
  ```
- The existing `4/4:Tempo` tag is **left in place** — the 4/4 feel is accurate for the song.
- The BPM tempo value is **not changed**.

Using:

```typescript
if (!song.tags?.some((t) => t.key === "12/8:Tempo")) {
  editor.addProperty(PropertyType.addedTags, "12/8:Tempo");
}
for (const dr of waltzRatings) {
  editor.addProperty(
    `${PropertyType.addedTags}:${dr.danceId}`,
    "Compound Time:Tempo",
  );
}
```

**Note:** 12/8 is used as a conventional label for compound duple/quadruple time even when the actual notation may differ. The `Compound Time:Tempo` dance tag provides the semantic flag for filtering/display.

**Result after save:** The song retains its 4/4 tag and BPM. The waltz dance rating gains a `Compound Time:Tempo` chip. The song also gains a `12/8:Tempo` song-level tag indicating compound time.

---

## New Component — `WaltzCorrectionCard.vue`

**Location:** `src/pages/song/components/WaltzCorrectionCard.vue`

**Props:**

```typescript
{
  song: Song;
  editor?: SongEditor;
  editing?: boolean;
  user?: string;
  canEdit?: boolean;
}
```

**Emits:** `edit` — signals SongCore to enter edit mode (same pattern as `DanceDetails` and `TagListEditor`).

**Visibility computed:**

```typescript
const WALTZ_IDS: string[] =
  safeDanceDatabase().groups.find((g) => g.id === "WLZ")?.danceIds ?? [];
const waltzRatings = computed(() =>
  (props.song.danceRatings ?? []).filter(
    (dr) => WALTZ_IDS.includes(dr.danceId) && dr.weight > 0,
  ),
);
const showCard = computed(
  () =>
    !!props.user &&
    props.canEdit &&
    !props.editing &&
    waltzRatings.value.length > 0 &&
    props.song.hasMeterTag(4),
);
```

**Tempo correction ratios (data, not code):** the config for Case 3 lives as a plain array local to the component. Adding a newly-observed miscount pattern means adding an entry here — nothing else in the component changes.

```typescript
interface TempoCorrectionCase {
  id: string;
  label: string; // what the algorithm likely did
  numerator: number;
  denominator: number;
}

const TEMPO_CORRECTION_CASES: TempoCorrectionCase[] = [
  {
    id: "4-to-3",
    label: "Counted 4 beats/measure (quarter notes) instead of 3",
    numerator: 3,
    denominator: 4,
  },
  {
    id: "2-to-3",
    label: "Counted 2 beats/measure (half notes) instead of 3",
    numerator: 3,
    denominator: 2,
  },
  { id: "div-3", label: "Tripled the true tempo", numerator: 1, denominator: 3 },
  {
    id: "mul-3",
    label: "Reported one-third of the true tempo",
    numerator: 3,
    denominator: 1,
  },
];

const tempoCorrectionOptions = computed(() =>
  TEMPO_CORRECTION_CASES.map((c) => {
    const correctedTempo = Math.round(
      (props.song.tempo! * c.numerator) / c.denominator,
    );
    return {
      ...c,
      correctedTempo,
      math: `${props.song.tempo} × ${c.numerator}/${c.denominator} = ${correctedTempo} BPM`,
    };
  }),
);
```

**Template:** A `<BCard>` with `border-variant="warning"` and a brief explanatory paragraph. Cases 1, 2, and 4 are each a single `<BButton>`. Case 3 instead renders a small table — one row per entry in `tempoCorrectionOptions`, showing the label, the math string, and an "Apply" button — so all candidate corrections are visible side by side for comparison against the manually-counted tempo:

```html
<table class="table table-sm mb-0">
  <tbody>
    <tr v-for="opt in tempoCorrectionOptions" :key="opt.id">
      <td>{{ opt.label }}</td>
      <td class="text-nowrap">{{ opt.math }}</td>
      <td>
        <BButton
          size="sm"
          variant="outline-danger"
          @click="applyTempoCorrection(opt)"
        >
          Apply
        </BButton>
      </td>
    </tr>
  </tbody>
</table>
```

**Button labels (Cases 1, 2, 4):**
| Button | Label                                                                 | Variant             |
| ------ | --------------------------------------------------------------------- | ------------------- |
| Case 1 | "Performer dances Waltz to 4/4 (Fake)"                                | `outline-secondary` |
| Case 2 | "Meter tag wrong — correct 4/4 → 3/4"                                 | `outline-warning`   |
| Case 4 | "Song has compound time — 4/4 feel with underlying waltz triple feel" | `outline-info`      |

Case 3 has no single button/label — see the table above; each row's "Apply" button carries its own math as the label.

**Handler sketch:**

```typescript
const applyFake = () => {
  for (const dr of waltzRatings.value) {
    props.editor!.addProperty(
      `${PropertyType.addedTags}:${dr.danceId}`,
      "Fake:Tempo",
    );
  }
  emit("edit");
};

const applyBad44 = () => {
  props.editor!.addProperty(PropertyType.removedTags, "4/4:Tempo");
  if (!props.song.hasMeterTag(3)) {
    props.editor!.addProperty(PropertyType.addedTags, "3/4:Tempo");
  }
};

const applyTempoCorrection = (opt: { correctedTempo: number }) => {
  applyBad44();
  props.editor!.modifyProperty(
    PropertyType.tempoField,
    opt.correctedTempo.toString(),
  );
  emit("edit");
};
```

(Note: `applyBad44` no longer emits `'edit'` itself — Case 2's button and `applyTempoCorrection` each emit after calling it, since Case 3 has additional work to do first.)

---

## Changes to `SongCore.vue`

Add a new `<BRow>` after the existing dances + stats row, containing a single `<BCol>` that renders `<WaltzCorrectionCard>`:

```html
<BRow v-if="editor && context.canEdit">
  <BCol>
    <WaltzCorrectionCard
      :song="song as Song"
      :editor="editor as SongEditor"
      :editing="editing"
      :user="model.userName"
      @edit="setEdit"
    />
  </BCol>
</BRow>
```

The `v-if="editor && context.canEdit"` guard keeps the row out of the DOM entirely for anonymous and non-`canEdit` users. `WaltzCorrectionCard` itself also guards internally on `showCard`, so it renders nothing when conditions aren't met — the `<BRow>` guard just avoids unnecessary component mounting.

---

## Files Affected

| File                                                | Change                                                 |
| --------------------------------------------------- | ------------------------------------------------------ |
| `src/pages/song/components/WaltzCorrectionCard.vue` | **New** — the correction card component                |
| `src/pages/song/components/SongCore.vue`            | Add `<WaltzCorrectionCard>` row after dances+stats row |

No model changes are required. All mutations use existing `SongEditor` public API (`addProperty`, `modifyProperty`) and existing `Song` public API (`hasMeterTag`, `danceRatings`).

---

## Open Questions / Future Work

- **Per-waltz granularity:** If a song has multiple waltz ratings (e.g., both SWZ and VWZ), Case 1 applies `Fake:Tempo` to all of them. If individual waltz tagging is needed later, the card could be expanded to show one row per waltz.
- **Undo:** All changes go through `SongEditor` and are reversible via "Cancel" before saving, or via "Undo My Changes" after saving.
- **Plausible-range filtering:** `TEMPO_CORRECTION_CASES` currently shows all four ratios unconditionally, even when a candidate's `correctedTempo` falls well outside any real waltz range. A future enhancement could dim or hide implausible rows using the WLZ dances' tempo ranges as a sanity check — purely a display filter, since the user's manual count is the real source of truth.
- **New failure modes:** If another miscount pattern turns up (e.g. a compound-meter mistake), add it to `TEMPO_CORRECTION_CASES` — no other code changes needed.
