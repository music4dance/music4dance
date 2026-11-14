import { describe, it, expect } from "vitest";
import { SongHistory } from "../SongHistory";
import { SongProperty } from "../SongProperty";

const A = "aaaaaaaa-0000-0000-0000-000000000001";
const B = "bbbbbbbb-0000-0000-0000-000000000002";
const C = "cccccccc-0000-0000-0000-000000000003";

const history = (props: [string, string][]): SongHistory =>
  new SongHistory({
    id: "merged",
    properties: props.map(([name, value]) => new SongProperty({ name, value })),
  });

const block = (
  action: string,
  value: string,
  user: string,
  time: string,
  ...rest: [string, string][]
) => [[action, value], ["User", user], ["Time", time], ...rest] as [string, string][];

const merged = (...extra: [string, string][]): SongHistory =>
  history([
    ...block(
      ".Create",
      A,
      "alice",
      "01/01/2020 10:00:00 AM",
      ["Title", "Song"],
      ["Artist", "Artist A"],
    ),
    ...block(
      ".Create",
      B,
      "bob",
      "01/02/2020 10:00:00 AM",
      ["Title", "Song"],
      ["Artist", "Artist B"],
    ),
    ...block(".Edit", B, "bob", "01/03/2020 10:00:00 AM", ["Artist", "Artist B2"]),
    ...block(
      ".Create",
      C,
      "carol",
      "01/04/2020 10:00:00 AM",
      ["Title", "Other"],
      ["Artist", "Artist C"],
    ),
    ...block(".Merge", `${A};${B};${C}`, "dwgray", "01/05/2020 10:00:00 AM"),
    ...extra,
  ]);

describe("SongHistory.mergeSources", () => {
  it("is empty for a song that was never merged", () => {
    expect(history(block(".Create", "", "alice", "01/01/2020 10:00:00 AM")).mergeSources).toEqual(
      [],
    );
  });

  it("lists each source with its creator, last title and artist, and block count", () => {
    const sources = merged().mergeSources;

    expect(sources.map((s) => s.id)).toEqual([A, B, C]);
    const b = sources[1]!;
    expect(b.user).toBe("bob");
    expect(b.title).toBe("Song");
    expect(b.artist).toBe("Artist B2");
    expect(b.edits).toBe(2);
    expect(b.created?.getFullYear()).toBe(2020);
  });

  it("ignores edits made after the merge", () => {
    const sources = merged(
      ...block(".Edit", "", "dwgray", "01/06/2020 10:00:00 AM", ["Artist", "Settled"]),
    ).mergeSources;

    expect(sources.map((s) => s.artist)).toEqual(["Artist A", "Artist B2", "Artist C"]);
  });

  it("leaves out sources a later .Unmerge removed", () => {
    const sources = merged(
      ...block(".Unmerge", C, "dwgray", "01/06/2020 10:00:00 AM"),
    ).mergeSources;

    expect(sources.map((s) => s.id)).toEqual([A, B]);
  });

  it("is empty once only one source is left", () => {
    expect(
      merged(...block(".Unmerge", `${B};${C}`, "dwgray", "01/06/2020 10:00:00 AM")).mergeSources,
    ).toEqual([]);
  });

  it("is empty for a merge whose blocks were never stamped", () => {
    const legacy = history([
      ...block(".Create", "", "alice", "01/01/2020 10:00:00 AM", ["Title", "Song"]),
      ...block(".Merge", `${A};${B}`, "dwgray", "01/05/2020 10:00:00 AM"),
    ]);

    expect(legacy.mergeSources).toEqual([]);
  });
});
