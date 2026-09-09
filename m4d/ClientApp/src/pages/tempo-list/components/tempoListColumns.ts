// Columns an advanced user can hide - "name" is always shown and isn't offered here. New optional
// columns should be added to this list with `defaultVisible: false` so casual users don't see
// their table layout change out from under them.
//
// A plain module (rather than living inside TempoList.vue's <script setup>) so App.vue can import
// `defaultVisibleColumns` too, to tell whether the current column selection is this default set -
// <script setup> can't have its own named exports, and App.vue needs this to decide whether
// `columns` belongs in the shareable URL at all (see App.vue's useUrlQuerySync call).
export interface ChooseableColumn {
  key: string;
  label: string;
  defaultVisible: boolean;
}

export const chooseableColumns: ChooseableColumn[] = [
  { key: "meter", label: "Meter", defaultVisible: true },
  { key: "bpm", label: "BPM", defaultVisible: true },
  { key: "mpm", label: "MPM", defaultVisible: true },
  { key: "groupName", label: "Type", defaultVisible: true },
  { key: "styles", label: "Styles", defaultVisible: true },
  { key: "validationRange", label: "Range", defaultVisible: false },
];

export const defaultVisibleColumns: string[] = chooseableColumns
  .filter((c) => c.defaultVisible)
  .map((c) => c.key);
