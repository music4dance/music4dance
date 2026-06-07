import { describe, it, expect, beforeEach } from "vitest";
import { Song } from "@/models/Song";
import { SongEditor } from "@/models/SongEditor";
import { SongHistory } from "@/models/SongHistory";
import { SongProperty } from "@/models/SongProperty";
import { DanceRating } from "@/models/DanceRating";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import { TypedJSON } from "typedjson";

setupTestEnvironment();

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Build a minimal SongHistory with the given raw properties. */
function makeHistory(properties: { name: string; value: string }[]): SongHistory {
  return new SongHistory({
    id: "00000000-0000-0000-0000-000000000001",
    properties: properties.map((p) => new SongProperty(p)),
  });
}

function makeBaseSong(): SongHistory {
  return makeHistory([
    { name: ".Create", value: "" },
    { name: "User", value: "dwgray" },
    { name: "Time", value: "01/01/2024 12:00:00 PM" },
    { name: "Tempo", value: "120" },
    { name: "DanceRating", value: "CHA+1" },
    { name: "DanceRating", value: "RMB+1" },
  ]);
}

// ---------------------------------------------------------------------------
// Song.loadProperties — Tempo+:DANCEID parsing
// ---------------------------------------------------------------------------

describe("Song per-dance tempo parsing", () => {
  it("parses Tempo+:DANCEID into DanceRating.tempo", () => {
    const history = makeHistory([
      ...makeBaseSong().properties,
      { name: ".Edit", value: "" },
      { name: "User", value: "dwgray" },
      { name: "Time", value: "01/01/2024 1:00:00 PM" },
      { name: "Tempo:CHA", value: "128" },
    ]);
    const song = Song.fromHistory(history);

    const cha = song.findDanceRatingById("CHA");
    expect(cha?.tempo).toBe(128);
    // RMB not overridden
    const rmb = song.findDanceRatingById("RMB");
    expect(rmb?.tempo).toBeUndefined();
  });

  it("promotes song tempo when Tempo:DanceId set and song has no tempo", () => {
    const history = makeHistory([
      { name: ".Create", value: "" },
      { name: "User", value: "dwgray" },
      { name: "Time", value: "01/01/2024 12:00:00 PM" },
      { name: "DanceRating", value: "CHA+1" },
      { name: "Tempo:CHA", value: "128" },
    ]);
    const song = Song.fromHistory(history);
    expect(song.tempo).toBe(128);
    expect(song.findDanceRatingById("CHA")?.tempo).toBe(128);
  });

  it("does not promote song tempo when song tempo already set", () => {
    const history = makeHistory([
      ...makeBaseSong().properties, // includes Tempo=120
      { name: ".Edit", value: "" },
      { name: "User", value: "dwgray" },
      { name: "Time", value: "01/01/2024 1:00:00 PM" },
      { name: "Tempo:CHA", value: "128" },
    ]);
    const song = Song.fromHistory(history);
    expect(song.tempo).toBe(120); // unchanged
    expect(song.findDanceRatingById("CHA")?.tempo).toBe(128);
  });

  it("second dance without override inherits promoted song tempo via fallback", () => {
    const history = makeHistory([
      { name: ".Create", value: "" },
      { name: "User", value: "dwgray" },
      { name: "Time", value: "01/01/2024 12:00:00 PM" },
      { name: "DanceRating", value: "CHA+1" },
      { name: "DanceRating", value: "RMB+1" },
      { name: "Tempo:CHA", value: "128" },
    ]);
    const song = Song.fromHistory(history);
    expect(song.tempo).toBe(128); // promoted
    const rmb = song.findDanceRatingById("RMB");
    expect(rmb?.tempo).toBeUndefined(); // no explicit override
    const effectiveRmb = rmb?.tempo ?? song.tempo;
    expect(effectiveRmb).toBe(128); // inherits via fallback
  });

  it("last Tempo write wins on multiple edits", () => {
    const history = makeHistory([
      ...makeBaseSong().properties,
      { name: ".Edit", value: "" },
      { name: "User", value: "dwgray" },
      { name: "Time", value: "01/01/2024 1:00:00 PM" },
      { name: "Tempo:CHA", value: "128" },
      { name: ".Edit", value: "" },
      { name: "User", value: "dwgray" },
      { name: "Time", value: "01/01/2024 2:00:00 PM" },
      { name: "Tempo:CHA", value: "132" },
    ]);
    const song = Song.fromHistory(history);
    expect(song.findDanceRatingById("CHA")?.tempo).toBe(132);
  });

  it("clears override when Tempo:DanceId value is empty", () => {
    const history = makeHistory([
      ...makeBaseSong().properties,
      { name: ".Edit", value: "" },
      { name: "User", value: "dwgray" },
      { name: "Time", value: "01/01/2024 1:00:00 PM" },
      { name: "Tempo:CHA", value: "128" },
      { name: ".Edit", value: "" },
      { name: "User", value: "dwgray" },
      { name: "Time", value: "01/01/2024 2:00:00 PM" },
      { name: "Tempo:CHA", value: "" },
    ]);
    const song = Song.fromHistory(history);
    expect(song.findDanceRatingById("CHA")?.tempo).toBeUndefined();
  });

  it("ignores Tempo:DanceId for unknown dance ID", () => {
    const history = makeHistory([
      ...makeBaseSong().properties,
      { name: ".Edit", value: "" },
      { name: "User", value: "dwgray" },
      { name: "Time", value: "01/01/2024 1:00:00 PM" },
      { name: "Tempo:XXX", value: "128" },
    ]);
    // Should not throw
    expect(() => Song.fromHistory(history)).not.toThrow();
  });

  it("ignores invalid Tempo:DanceId values instead of writing NaN", () => {
    const history = makeHistory([
      ...makeBaseSong().properties,
      { name: ".Edit", value: "" },
      { name: "User", value: "dwgray" },
      { name: "Time", value: "01/01/2024 1:00:00 PM" },
      { name: "Tempo:CHA", value: "not-a-number" },
    ]);

    const song = Song.fromHistory(history);
    expect(song.findDanceRatingById("CHA")?.tempo).toBeUndefined();
    expect(song.tempo).toBe(120);
  });

  it("typedjson round-trips DanceRating.tempo", () => {
    const serializer = new TypedJSON(Song);
    const source = new Song({
      tempo: 171,
      danceRatings: [new DanceRating({ danceId: "VWZ", weight: 1, tempo: 214 })],
    });

    const json = serializer.stringify(source);
    const parsed = serializer.parse(json);

    expect(parsed?.danceRatings?.[0]?.tempo).toBe(214);
  });
});

// ---------------------------------------------------------------------------
// SongEditor.setDanceTempo
// ---------------------------------------------------------------------------

describe("SongEditor.setDanceTempo", () => {
  let editor: SongEditor;

  beforeEach(() => {
    editor = new SongEditor(undefined, "dwgray", makeBaseSong());
  });

  it("produces a Tempo:DANCEID property", () => {
    editor.setDanceTempo("CHA", "128");
    const props = editor.editHistory.properties;
    const tempoProp = props.find((p) => p.name === "Tempo:CHA");
    expect(tempoProp).toBeDefined();
    expect(tempoProp?.value).toBe("128");
  });

  it("updating twice produces one property (modifyProperty deduplicates)", () => {
    editor.setDanceTempo("CHA", "128");
    editor.setDanceTempo("CHA", "132");
    const props = editor.editHistory.properties;
    const tempoProps = props.filter((p) => p.name === "Tempo:CHA");
    expect(tempoProps).toHaveLength(1);
    expect(tempoProps[0].value).toBe("132");
  });

  it("clears override when called with undefined", () => {
    editor.setDanceTempo("CHA", "128");
    editor.setDanceTempo("CHA", undefined);
    const props = editor.editHistory.properties;
    const tempoProp = props.find((p) => p.name === "Tempo:CHA");
    expect(tempoProp?.value).toBe("");
  });

  it("different dance IDs produce separate properties", () => {
    editor.setDanceTempo("CHA", "128");
    editor.setDanceTempo("RMB", "108");
    const props = editor.editHistory.properties;
    expect(props.find((p) => p.name === "Tempo:CHA")?.value).toBe("128");
    expect(props.find((p) => p.name === "Tempo:RMB")?.value).toBe("108");
  });
});
