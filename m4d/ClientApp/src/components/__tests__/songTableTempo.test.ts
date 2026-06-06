import { describe, expect, it } from "vitest";
import { Song } from "@/models/Song";
import { DanceRating } from "@/models/DanceRating";
import { displayTempoForSongTable } from "@/components/songTableTempo";

describe("displayTempoForSongTable", () => {
  it("uses per-dance tempo for a single-dance context", () => {
    const song = new Song({
      tempo: 171,
      danceRatings: [new DanceRating({ danceId: "VWZ", weight: 1, tempo: 214 })],
    });

    expect(displayTempoForSongTable(song, "VWZ", false)).toBe("214");
  });

  it("falls back to song tempo when no dance override exists", () => {
    const song = new Song({
      tempo: 171,
      danceRatings: [new DanceRating({ danceId: "VWZ", weight: 1 })],
    });

    expect(displayTempoForSongTable(song, "VWZ", false)).toBe("171");
    expect(displayTempoForSongTable(song, "FXT", false)).toBe("171");
  });

  it("returns empty when tempo column is hidden", () => {
    const song = new Song({
      tempo: 171,
      danceRatings: [new DanceRating({ danceId: "VWZ", weight: 1, tempo: 214 })],
    });

    expect(displayTempoForSongTable(song, "VWZ", true)).toBe("");
  });
});
