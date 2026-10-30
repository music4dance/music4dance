import { describe, expect, it } from "vitest";
import { Song } from "../Song";
import { SongHistory } from "../SongHistory";
import { SongProperty } from "../SongProperty";
import cases from "./album-trim-cases.json";

// Shared with m4dModels.Tests/SongHistoryTrimTests.cs, which checks that
// SongHistory.TrimAlbumsForList turns each 'log' into its 'trimmed' log. This side checks that
// a song list row can't tell the two apart.

function songFromLog(log: string[]): Song {
  const properties = log.map((entry) => {
    const eq = entry.indexOf("=");
    return new SongProperty({ name: entry.slice(0, eq), value: entry.slice(eq + 1) });
  });
  return Song.fromHistory(new SongHistory({ id: "album-trim-test", properties }));
}

// Everything a song-list row reads from a song (SongTable, PlayModal and their children).
function listRow(song: Song) {
  return {
    title: song.title,
    artist: song.artist,
    tempo: song.tempo,
    created: song.created,
    modified: song.modified,
    edited: song.edited,
    modifiedBy: song.modifiedBy,
    danceRatings: song.danceRatings,
    tags: song.tags,
    firstAlbum: song.albums?.[0]?.name,
    track: song.albums?.[0]?.track,
    purchases: song.getPurchaseInfos().map((p) => p.link),
  };
}

const trimmedCases = cases.cases.filter((c) => c.trimmed !== null);

describe("album trim for anonymous song lists", () => {
  it.each(trimmedCases.map((c) => [c.name, c] as const))("%s", (_, c) => {
    const full = songFromLog(c.log);
    const trimmed = songFromLog(c.trimmed!);

    expect(listRow(trimmed)).toEqual(listRow(full));
  });

  it("drops albums in the cases that have extras", () => {
    const c = trimmedCases[0]!;

    expect(songFromLog(c.trimmed!).albums!.length).toBeLessThan(songFromLog(c.log).albums!.length);
  });
});
