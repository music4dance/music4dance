import { loadTestDances } from "@/helpers/LoadTestDances";
import { DanceDatabase } from "../DanceDatabase/DanceDatabase";
import { SongFilter } from "../SongFilter";
import { beforeAll, describe, expect, it } from "vitest";
import { loadDancesFromString } from "@/helpers/DanceLoader";
import { setupTestEnvironment } from "@/helpers/TestHelpers";

const simple = "Advanced--Modified";
const basic = "Advanced--Modified---\\-me|h";
const complex = "Advanced-ATN,BBA-Dances--A-\\-me|h-100-150--+Big Band:Music|\\-Swing:Music-3";
const complex2 =
  "v2-Advanced-ATN,BBA-Dances--A-\\-me|h-100-150-90-180--+Big Band:Music|\\-Swing:Music-3";

declare global {
  interface Window {
    danceDatabase: DanceDatabase | undefined;
  }
}

setupTestEnvironment();

describe("song filter", () => {
  beforeAll(() => {
    window.danceDatabase = loadDancesFromString(loadTestDances());
  });
  it("should load basic from string", () => {
    const f = SongFilter.buildFilter(basic);

    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.sortOrder).toEqual("Modified");
    expect(f.user).toEqual("-me|h");
    expect(f.purchase).toBeUndefined();
  });

  it("should load complicated from string", () => {
    const f = SongFilter.buildFilter(complex);

    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.dances).toEqual("ATN,BBA");
    expect(f.sortOrder).toEqual("Dances");
    expect(f.purchase).toEqual("A");
    expect(f.user).toEqual("-me|h");
    expect(f.tempoMin).toEqual(100);
    expect(f.tempoMax).toEqual(150);
  });

  it("should load complicated2 from string", () => {
    const f = SongFilter.buildFilter(complex2);

    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.dances).toEqual("ATN,BBA");
    expect(f.sortOrder).toEqual("Dances");
    expect(f.purchase).toEqual("A");
    expect(f.user).toEqual("-me|h");
    expect(f.tempoMin).toEqual(100);
    expect(f.tempoMax).toEqual(150);
    expect(f.lengthMin).toEqual(90);
    expect(f.lengthMax).toEqual(180);
  });

  it("should pass isEmpty on a trivial filter", () => {
    const f = SongFilter.buildFilter("");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeTruthy();
  });

  it("should pass isEmpty on a simple filter", () => {
    const f = SongFilter.buildFilter(simple);
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeTruthy();
  });

  it("should fail isEmpty on a basic filter", () => {
    const f = SongFilter.buildFilter(basic);
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
  });

  it("should describe a dance/user filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN----\\-me|h");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual("All Argentine Tango songs excluding songs in my blocked list.");
  });

  it("should describe a dance/user filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BOL----\\-me|h");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Bolero excluding songs in my blocked list.",
    );
  });

  it("should describe a dance/tempo filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BBA-----100-150--");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Balboa having" +
        " tempo between 100 and 150 beats per minute.",
    );
  });

  it("should describe a dance/min-tempo filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BBA-Dances_desc----100---");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Balboa having" +
        " tempo greater than 100 beats per minute sorted by Dance Rating from least popular to most popular.",
    );
  });

  it("should describe a dance/max-tempo filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BBA-Dances-----150--");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Balboa having" +
        " tempo less than 150 beats per minute sorted by Dance Rating from most popular to least popular.",
    );
  });

  it("should describe a full purchase filter", () => {
    const f = SongFilter.buildFilter("Advanced----AIS-");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual("All songs available on Amazon or ITunes or Spotify.");
  });

  it("should describe a simple purchase filter", () => {
    const f = SongFilter.buildFilter("Advanced----S-");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual("All songs available on Spotify.");
  });

  it("should describe a dance/min-tempo/purchase filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BBA-Dances_desc--S--100-150--");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Balboa" +
        " available on Spotify" +
        " having tempo between 100 and 150 beats per minute" +
        " sorted by Dance Rating from least popular to most popular.",
    );
  });

  it("should describe a dance/keyword/min-tempo/purchase filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BBA-Dances_desc-LOVE-S--100-150--");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Balboa" +
        ' containing the text "LOVE"' +
        " available on Spotify" +
        " having tempo between 100 and 150 beats per minute" +
        " sorted by Dance Rating from least popular to most popular.",
    );
  });

  it("should describe a complex filter", () => {
    const f = SongFilter.buildFilter(complex);
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Balboa" +
        " available on Amazon" +
        " including tag Big Band" +
        " excluding tag Swing" +
        " having tempo between 100 and 150 beats per minute" +
        " excluding songs in my blocked list" +
        " sorted by Dance Rating from most popular to least popular.",
    );
  });

  it("should describe a complex2 filter", () => {
    const f = SongFilter.buildFilter(complex2);
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Balboa" +
        " available on Amazon" +
        " including tag Big Band" +
        " excluding tag Swing" +
        " having tempo between 100 and 150 beats per minute" +
        " having length between 90 and 180 seconds" +
        " excluding songs in my blocked list" +
        " sorted by Dance Rating from most popular to least popular.",
    );
  });
});
