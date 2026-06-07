import { describe, expect, it } from "vitest";
import { Song } from "@/models/Song";
import { DanceRating } from "@/models/DanceRating";
import { displayTempoForSongTable } from "@/components/songTableTempo";

describe("SongTable tempo display", () => {
  it("shows dance-specific tempo when filter is a single concrete dance", () => {
    const song = new Song({
      tempo: 171,
      danceRatings: [new DanceRating({ danceId: "VWZ", weight: 1, tempo: 214 })],
    });

    expect(displayTempoForSongTable(song, "VWZ", false)).toBe("214");
  });

  it("falls back to song tempo when filter is a dance group", () => {
    const song = new Song({
      tempo: 171,
      danceRatings: [new DanceRating({ danceId: "VWZ", weight: 1, tempo: 214 })],
    });

    expect(displayTempoForSongTable(song, "FXT", false)).toBe("171");
  });

  it("falls back to song tempo when no dance override exists", () => {
    const song = new Song({
      tempo: 171,
      danceRatings: [new DanceRating({ danceId: "VWZ", weight: 1 })],
    });

    expect(displayTempoForSongTable(song, "VWZ", false)).toBe("171");
  });
});
