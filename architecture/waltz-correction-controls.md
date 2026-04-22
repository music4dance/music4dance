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

> The algorithm identified the beat correctly but counted 4 beats per measure instead of 3, so the reported BPM is 4/3 × the correct value. Correcting requires adjusting the tempo as well as the meter tag.

**Mutations:**
Same as Case 2, plus:

- Multiply the current tempo by 3/4 and round to nearest integer:
  ```
  Tempo = round(song.tempo × 3 / 4)
  ```
  Using `editor.modifyProperty(PropertyType.tempoField, correctedTempo.toString())`.

```typescript
editor.addProperty(PropertyType.removedTags, "4/4:Tempo");
if (!song.hasMeterTag(3)) {
  editor.addProperty(PropertyType.addedTags, "3/4:Tempo");
}
const correctedTempo = Math.round((song.tempo! * 3) / 4);
editor.modifyProperty(PropertyType.tempoField, correctedTempo.toString());
```

**Example:** A song at 120 BPM tagged 4/4 is likely a 90 BPM waltz. After correction: 90 BPM, `3/4:Tempo`.

**Result after save:** Both the meter tag and the BPM value are corrected.

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
const waltzIds = ["SWZ", "CSW", "VWZ", "TGV"];
const waltzRatings = computed(() =>
  (props.song.danceRatings ?? []).filter(
    (dr) => waltzIds.includes(dr.danceId) && dr.weight > 0,
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

**Template:** A `<BCard>` with `border-variant="warning"` and a brief explanatory paragraph, followed by three `<BButton>` elements (one per case). Each button calls its handler, then emits `'edit'`.

**Button labels (suggested):**
| Button | Label | Variant |
|--------|-------|---------|
| Case 1 | "Performer dances Waltz to 4/4 (Fake)" | `outline-secondary` |
| Case 2 | "Meter tag wrong — correct 4/4 → 3/4" | `outline-warning` |
| Case 3 | "Meter + tempo wrong — correct 4/4 → 3/4 and adjust BPM" | `outline-danger` || Case 4 | "Song has compound time — 4/4 feel with underlying waltz triple feel" | `outline-info` |
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
  emit("edit");
};

const applyBad44AndTempo = () => {
  applyBad44();
  const correctedTempo = Math.round((props.song.tempo! * 3) / 4);
  props.editor!.modifyProperty(
    PropertyType.tempoField,
    correctedTempo.toString(),
  );
};
```

(Note: `applyBad44AndTempo` calls `applyBad44` which already emits `'edit'`, so no duplicate emit needed.)

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
- **Tempo display preview:** A future enhancement could show the computed corrected tempo (e.g., "120 BPM → 90 BPM") inline in the Case 3 button so users can verify before applying.
- **Undo:** All changes go through `SongEditor` and are reversible via "Cancel" before saving, or via "Undo My Changes" after saving.
