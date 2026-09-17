import { describe, expect, it } from "vitest";
import { Song } from "../Song";
import { SongHistory } from "../SongHistory";
import { PropertyType, SongProperty } from "../SongProperty";
import { SongEditor } from "../SongEditor";
import { ServiceName, TrackModel } from "../TrackModel";
import {
  artistCreditLayout,
  artistKey,
  cleanArtistName,
  collaborators,
  deserializeArtists,
  serializeArtists,
} from "../ArtistNames";
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

describe("artistCreditLayout", () => {
  it("links each individual artist in place", () => {
    const layout = artistCreditLayout("Dolly Parton & Kenny Rogers", [
      "Dolly Parton",
      "Kenny Rogers",
    ]);
    expect(layout.segments).toEqual([
      { text: "Dolly Parton", artist: "Dolly Parton" },
      { text: " & " },
      { text: "Kenny Rogers", artist: "Kenny Rogers" },
    ]);
    expect(layout.extra).toEqual([]);
  });

  it("matches case-insensitively and keeps the credit's spelling", () => {
    const layout = artistCreditLayout("PITBULL feat. Ne-Yo", ["Pitbull", "Ne-Yo"]);
    expect(layout.segments[0]).toEqual({ text: "PITBULL", artist: "Pitbull" });
    expect(layout.segments[2]).toEqual({ text: "Ne-Yo", artist: "Ne-Yo" });
  });

  it("returns artists missing from the credit as extras", () => {
    const layout = artistCreditLayout("Lindsey Stirling", ["Lindsey Stirling", "ZZ Ward"]);
    expect(layout.segments).toEqual([{ text: "Lindsey Stirling", artist: "Lindsey Stirling" }]);
    expect(layout.extra).toEqual(["ZZ Ward"]);
  });

  it("doesn't overlap matches", () => {
    const layout = artistCreditLayout("Tony Evans & Tony Evans Orchestra", [
      "Tony Evans Orchestra",
      "Tony Evans",
    ]);
    expect(layout.segments.filter((s) => s.artist).map((s) => s.text)).toEqual([
      "Tony Evans",
      "Tony Evans Orchestra",
    ]);
  });
});

describe("SongEditor artists", () => {
  const baseLog = [
    ".Create=",
    "User=dwgray",
    "Time=01/15/2024 14:30:00",
    "Title=Islands in the Stream",
    "Artist=Dolly Parton",
  ];

  function editor(): SongEditor {
    const properties = baseLog.map((entry) => {
      const eq = entry.indexOf("=");
      return new SongProperty({ name: entry.slice(0, eq), value: entry.slice(eq + 1) });
    });
    return new SongEditor(undefined, "alice", new SongHistory({ id: "s1", properties }));
  }

  it("keeps a new Artists edit when the credit is edited afterwards", () => {
    const e = editor();
    e.setArtists(["Dolly Parton", "Kenny Rogers"]);
    e.modifyProperty(PropertyType.artistField, "Dolly Parton & Kenny Rogers");

    const names = e.editHistory.properties.map((p) => p.baseName);
    expect(names.indexOf(PropertyType.artistsField)).toBeGreaterThan(
      names.indexOf(PropertyType.artistField),
    );
    expect(e.song.artists).toEqual(["Dolly Parton", "Kenny Rogers"]);
    expect(e.song.artistsSource).toBe("User");
  });

  it("clears the list when reset to automatic", () => {
    const e = editor();
    e.setArtists(["Dolly Parton", "Kenny Rogers"]);
    e.setArtists(undefined);
    expect(e.song.artists).toBeUndefined();
    expect(e.song.artistsSource).toBe("None");
  });
});

describe("artistKey and collaborators", () => {
  it.each(cases.artistKey.map(([input, expected]) => [input, expected] as const))(
    "keys %j as %j",
    (input, expected) => {
      expect(artistKey(input)).toBe(expected);
    },
  );

  it("ignores case, spacing and diacritics", () => {
    expect(artistKey(" Michael  Bublé ")).toBe(artistKey("michael buble"));
    expect(artistKey("Céline Dion")).toBe("celine dion");
  });

  it("counts co-credited artists, grouping spelling variants", () => {
    const songs = [
      { effectiveArtists: ["Michael Bublé", "Meghan Trainor"] },
      { effectiveArtists: ["Michael Buble", "Meghan Trainor"] },
      { effectiveArtists: ["michael bublé", "Barry Manilow"] },
      { effectiveArtists: ["Meghan Trainor", "John Legend"] },
      { effectiveArtists: ["Michael Bublé"] },
    ];
    expect(collaborators("Michael Buble", songs)).toEqual([
      { artist: "Meghan Trainor", count: 2 },
      { artist: "Barry Manilow", count: 1 },
    ]);
  });
});

describe("SongHistory.fromTrack artists", () => {
  const track = (): TrackModel => new TrackModel();

  it("records structured service credits as a service Artists list", () => {
    const t = Object.assign(track(), {
      service: ServiceName.Spotify,
      trackId: "t1",
      name: "Islands in the Stream",
      collectionId: "c1",
      artist: "Dolly Parton",
      artists: ["Dolly Parton", "Kenny Rogers"],
      album: "Greatest Hits",
    });
    const song = Song.fromHistory(SongHistory.fromTrack(undefined!, t, "alice"));
    expect(song.artist).toBe("Dolly Parton");
    expect(song.artists).toEqual(["Dolly Parton", "Kenny Rogers"]);
    // fromTrack currently folds the service's edits into the creating user's block (setupEdit
    // doesn't open a second block), so the list is attributed to the user rather than batch-s
    expect(song.artistsSource).toBe("User");
  });

  it("doesn't record a single-artist credit", () => {
    const t = Object.assign(track(), {
      service: ServiceName.Spotify,
      trackId: "t1",
      name: "Jolene",
      collectionId: "c1",
      artist: "Dolly Parton",
      artists: ["Dolly Parton"],
      album: "Jolene",
    });
    const history = SongHistory.fromTrack(undefined!, t, "alice");
    expect(history.properties.some((p) => p.baseName === PropertyType.artistsField)).toBe(false);
  });
});
