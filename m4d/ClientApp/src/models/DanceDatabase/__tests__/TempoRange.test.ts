import { describe, expect, test } from "vitest";
import { TempoRange } from "../TempoRange";

describe("TempoRange.calculatePitchPercent", () => {
  const range = new TempoRange(100, 128);

  test("is zero for a tempo inside the range", () => {
    expect(range.calculatePitchPercent(120)).toBe(0);
  });

  test("is negative (slow down) for a song faster than the range, relative to the song's tempo", () => {
    // 128 / 140 = 91.43% playback speed, i.e. -8.57% on the pitch control. The edge-relative
    // calculateDeltaPercent gives 9.375% for the same tempo, which would overshoot.
    expect(range.calculatePitchPercent(140)).toBeCloseTo(-8.571, 3);
    expect((140 * (100 + range.calculatePitchPercent(140))) / 100).toBeCloseTo(128, 6);
  });

  test("is positive (speed up) for a song slower than the range", () => {
    // 100 / 80 = 125% playback speed.
    expect(range.calculatePitchPercent(80)).toBeCloseTo(25, 6);
  });

  test("is zero for a zero tempo rather than dividing by zero", () => {
    expect(range.calculatePitchPercent(0)).toBe(0);
  });
});
