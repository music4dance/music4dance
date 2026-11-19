// The ways a near-miss dance's tempo difference can be shown next to it in the results list.
// A plain module (rather than living inside a <script setup>) so App.vue, DanceDeltas.vue and
// TempoDeltaInfo.vue can share the keys, and App.vue can tell whether the current selection is the
// default (to decide whether `show` belongs in the shareable URL at all).
//
// - tempo: the raw difference, "1.0 MPM fast" / "2.3 BPM slow"
// - pitch: the setting for a CD player's or DJ deck's pitch fader, signed: "Pitch −8.6%"
// - speed: the playback rate tempo-change software asks for, 100% = unchanged: "Speed 91.4%"
export type DeltaMetric = "tempo" | "pitch" | "speed";

export interface DeltaMetricOption {
  key: DeltaMetric;
  label: string;
  defaultVisible: boolean;
}

export const deltaMetrics: DeltaMetricOption[] = [
  { key: "tempo", label: "Tempo", defaultVisible: true },
  { key: "pitch", label: "Pitch", defaultVisible: true },
  { key: "speed", label: "Speed", defaultVisible: false },
];

export const defaultDeltaMetrics: DeltaMetric[] = deltaMetrics
  .filter((m) => m.defaultVisible)
  .map((m) => m.key);

// Same "silently drop invalid seeded values" convention as QueryValidation.ts. A missing `?show=`
// falls back to the default set. An explicit selection whose keys are all invalid yields an empty
// list, the same as Tempo List's `?columns=`.
export function validateDeltaMetrics(values?: string[]): DeltaMetric[] {
  if (!values) {
    return defaultDeltaMetrics;
  }
  return deltaMetrics.map((m) => m.key).filter((key) => values.includes(key));
}
