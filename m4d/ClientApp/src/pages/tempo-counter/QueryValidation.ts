import type { CountMethod } from "./CountMethod";

// Meters this page's UI ever offers (MeasuresPerMinute.vue's dropdown) - the only numerators
// that DanceDeltas.vue's meter pre-filter knows how to match against.
export const VALID_NUMERATORS = [2, 3, 4];
export const DEFAULT_NUMERATOR = 4;

// Matches TempoModal.vue's manual-entry input bounds (min="0" max="500").
export const MIN_TEMPO = 0;
export const MAX_TEMPO = 500;
export const DEFAULT_TEMPO = 0;

export const DEFAULT_COUNT_METHOD: CountMethod = "beats";

// Matches StrictSlider.vue's range input bounds (min="0" max="20").
export const MIN_EPSILON = 0;
export const MAX_EPSILON = 20;
export const DEFAULT_EPSILON = 5;

// The four validators below all follow the same "silently drop an invalid seeded value and fall
// back to the default" convention the Tempo List page uses for its own query-string-seeded
// filters (filterValid/filterValidMeters in CheckboxTypes.ts) - a malformed or out-of-range
// ?numerator=/?tempo=/?count=/?epsilon= should never reach the rest of the page's state.

export function validateNumerator(value?: number): number {
  return value !== undefined && VALID_NUMERATORS.includes(value) ? value : DEFAULT_NUMERATOR;
}

export function validateTempo(value?: number): number {
  return value !== undefined && Number.isFinite(value) && value >= MIN_TEMPO && value <= MAX_TEMPO
    ? value
    : DEFAULT_TEMPO;
}

export function validateCountMethod(value?: string): CountMethod {
  return value === "measures" ? "measures" : DEFAULT_COUNT_METHOD;
}

export function validateEpsilon(value?: number): number {
  return value !== undefined &&
    Number.isFinite(value) &&
    value >= MIN_EPSILON &&
    value <= MAX_EPSILON
    ? value
    : DEFAULT_EPSILON;
}
