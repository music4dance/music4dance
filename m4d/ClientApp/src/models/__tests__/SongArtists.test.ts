import { describe, expect, it } from "vitest";
import { Song } from "../Song";
import { SongHistory } from "../SongHistory";
import { SongProperty } from "../SongProperty";
import { cleanArtistName, deserializeArtists, serializeArtists } from "../ArtistNames";
import cases from "./artists-replay-cases.json";

// Shared with m4dModels.Tests/ArtistsPropertyTests.cs so the C# and TypeScript replays can't drift.

function songFromLog(log: string[]): Song {
  const properties = log.map((entry) => {
    const eq = entry.indexOf("=");
    return new SongProperty({ name: entry.slice(0, eq), value: entry.slice(eq + 1) });
  });
  return Song.fromHistory(new SongHistory({ id: "artists-test", properties }));
}

describe("Song Artists property replay", () => {
  it.each(cases.cases.map((c) => [c.name, c] as const))("%s", (_, c) => {
    const song = songFromLog(c.log);
    expect(song.artists ?? null).toEqual(c.artists);
    expect(song.effectiveArtists).toEqual(c.effective);
    expect(song.artistsSource).toBe(c.source);
  });

  it("reports whether individual artists add information", () => {
    expect(songFromLog(cases.cases[0]!.log).hasIndividualArtists).toBe(false);
    expect(songFromLog(cases.cases[2]!.log).hasIndividualArtists).toBe(true);
  });
});

describe("ArtistNames", () => {
  it.each(cases.cleanName.map(([input, expected]) => [input, expected] as const))(
    "cleans %j to %j",
    (input, expected) => {
      expect(cleanArtistName(input)).toBe(expected);
    },
  );

  it("round trips lists", () => {
    const value = serializeArtists(["Dolly Parton", " Kenny  Rogers ", "", "A|B"]);
    expect(value).toBe("Dolly Parton|Kenny Rogers|A/B");
    expect(deserializeArtists(value)).toEqual(["Dolly Parton", "Kenny Rogers", "A/B"]);
    expect(deserializeArtists("")).toEqual([]);
    expect(deserializeArtists(undefined)).toEqual([]);
  });
});
