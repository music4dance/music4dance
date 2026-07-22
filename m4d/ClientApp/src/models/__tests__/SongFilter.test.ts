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

  it("should describe a dance/blocked filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN----\\-me|h");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All Argentine Tango songs excluding songs in my blocked list sorted by Dance Rating from most popular to least popular.",
    );
  });

  it("should describe a multi-dance/blocked filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BOL----\\-me|h");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Bolero excluding songs in my blocked list " +
        "sorted by Dance Rating from most popular to least popular.",
    );
  });

  it("should describe a dance/tempo filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BBA-----100-150--");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Balboa having" +
        " tempo between 100 and 150 beats per minute sorted by Dance Rating from most popular to least popular.",
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

  it("should describe a single-dance tempo filter using dance-specific tempo wording", () => {
    const f = SongFilter.buildFilter("Advanced-CHA-----100-150--");
    expect(f).toBeDefined();
    expect(f.singleDance).toBe(true);
    expect(f.description).toContain(
      "having for Cha Cha tempo between 100 and 150 beats per minute",
    );
  });

  it("should describe a multi-dance tempo filter using the marked scope dance's name", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BBA*-----100-150--");
    expect(f.description).toContain("having for Balboa tempo between 100 and 150 beats per minute");
  });

  it("should note the scope dance for a dance-rating sort when explicitly marked", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BBA*-------");
    expect(f.description).toContain(
      "sorted by Dance Rating from most popular to least popular. Using Balboa for rating and tempo.",
    );
  });

  it("should note the scope dance for a tempo sort when explicitly marked", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BBA*-Tempo------");
    expect(f.description).toContain(
      "sorted by Tempo from slowest to fastest. Using Balboa for rating and tempo.",
    );
  });

  it("should not note a scope dance when no item is marked", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BBA-------");
    expect(f.description).not.toContain("Using ");
  });

  it("should not note a scope dance for a single-dance filter with a stale marker", () => {
    // Mirrors a leftover '*' from deselecting the other dances after marking one - already
    // covered by the "All Balboa songs" prefix, so the trailing note would be redundant.
    const f = SongFilter.buildFilter("Advanced-BBA*-------");
    expect(f.singleDance).toBe(true);
    expect(f.description).not.toContain("Using ");
  });

  it("should not duplicate the scope note when a tempo range already names the dance", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BBA*-----100-150--");
    expect(f.description).toContain("having for Balboa tempo between 100 and 150 beats per minute");
    expect(f.description).not.toContain("Using ");
  });

  it("should describe a full purchase filter", () => {
    const f = SongFilter.buildFilter("Advanced----AIS-");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs available on Amazon or ITunes or Spotify sorted by Dance Rating from most popular to least popular.",
    );
  });

  it("should describe a simple purchase filter", () => {
    const f = SongFilter.buildFilter("Advanced----S-");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs available on Spotify sorted by Dance Rating from most popular to least popular.",
    );
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

  it("should route raw azure searches through azuresearch, not the raw action verbatim", () => {
    const f = new SongFilter();
    f.action = "azure+raw+";
    f.dances = "dance_ALL/Tempo ne null";

    expect(f.isRaw).toBe(true);
    expect(f.isAzure).toBe(true);
    expect(f.targetAction).toEqual("azuresearch");
    expect(f.url).toMatch(/^\/song\/azuresearch\?filter=/);
  });

  it("should round-trip a purchase filter with an exclusion through the filter string", () => {
    // "SNR" = available on Spotify, not available on ISRC ('N' splits include/exclude).
    const f = SongFilter.buildFilter("Advanced----SNR-");
    expect(f.purchase).toEqual("SNR");

    const reparsed = SongFilter.buildFilter(f.query);
    expect(reparsed.purchase).toEqual("SNR");
  });

  it("should split a purchase filter into include/exclude parts", () => {
    expect(SongFilter.splitPurchase(undefined)).toEqual({});
    expect(SongFilter.splitPurchase("IS")).toEqual({ include: "IS" });
    expect(SongFilter.splitPurchase("SNR")).toEqual({ include: "S", exclude: "R" });
    expect(SongFilter.splitPurchase("NIS")).toEqual({ include: undefined, exclude: "IS" });
  });

  it("should join include/exclude parts into a purchase filter", () => {
    expect(SongFilter.joinPurchase(["S"], ["R"])).toEqual("SNR");
    expect(SongFilter.joinPurchase(["I", "S"], [])).toEqual("IS");
    expect(SongFilter.joinPurchase([], ["I", "S"])).toEqual("NIS");
    expect(SongFilter.joinPurchase([], [])).toBeUndefined();
  });

  it("should describe a purchase filter with an exclusion", () => {
    const f = SongFilter.buildFilter("Advanced----SNR-");
    expect(f.description).toEqual(
      "All songs available on Spotify" +
        " not available on ISRC" +
        " sorted by Dance Rating from most popular to least popular.",
    );
  });

  it("should normalize case and spaces when checking isRaw", () => {
    const f = new SongFilter();
    f.action = "Azure Raw Lucene";

    expect(f.isRaw).toBe(true);
  });
});
