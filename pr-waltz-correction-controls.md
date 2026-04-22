# PR: Waltz 4/4 Correction Controls

## Summary

Adds a one-click correction card to the song details page for `canEdit` users when a song has a waltz dance rating and a conflicting `4/4:Tempo` tag. Waltzes are by definition 3/4, so this combination is almost always a data error introduced by the automated tempo algorithm.

## Changes

### New component — `WaltzCorrectionCard.vue`

Displayed after the dance ratings row when:

- The user is authenticated and has `canEdit`
- The song has at least one positive waltz dance rating (any dance in the **WLZ group**: CSW, SWZ, VWZ, TGV)
- The song has a `4/4:Tempo` tag
- The page is not already in edit mode

Offers four one-click corrections:

| Button                    | Action                                                                                          |
| ------------------------- | ----------------------------------------------------------------------------------------------- |
| **Mark as Fake**          | Adds `Fake:Tempo` dance tag to each waltz rating; leaves 4/4 in place                           |
| **Correct meter**         | Swaps `4/4:Tempo` → `3/4:Tempo`; tempo BPM unchanged                                            |
| **Correct meter + tempo** | Swaps `4/4:Tempo` → `3/4:Tempo` and multiplies BPM × ¾                                          |
| **Compound time**         | Adds `12/8:Tempo` song tag + `Compound Time:Tempo` dance tag to each waltz; leaves 4/4 in place |

Waltz IDs are derived at runtime from the **WLZ dance group** in `dancegroups.json` — no hardcoded list.

### Wired into `SongCore.vue`

The card is rendered in a new row below the dances/stats section, receiving `song`, `editor`, `editing`, `user`, and `canEdit` props.

### New tags added to `tags.json`

- `12/8:Tempo`
- `Compound Time:Tempo`

### Architecture doc — `architecture/waltz-correction-controls.md`

Documents the detection conditions, all four cases, and the WLZ group derivation approach.

### Tests — `WaltzCorrectionCard.test.ts`

27 tests covering visibility (8), and each of the four correction cases including multi-waltz songs, duplicate-tag guards, and TGV regression cases.

## Testing

All client tests pass (`yarn vitest run`).
