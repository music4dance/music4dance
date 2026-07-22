import { beforeAll, describe, expect, it } from "vitest";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import { loadTestDances } from "@/helpers/LoadTestDances";
import { loadDancesFromString } from "@/helpers/DanceLoader";
import { DanceDatabase } from "../DanceDatabase/DanceDatabase";
import { DanceQuery } from "../DanceQuery";

declare global {
  interface Window {
    danceDatabase: DanceDatabase | undefined;
  }
}

setupTestEnvironment();

describe("DanceQuery", () => {
  beforeAll(() => {
    window.danceDatabase = loadDancesFromString(loadTestDances());
  });

  it("has no primary dance when no item is marked", () => {
    const q = new DanceQuery("ATN,BBA");
    expect(q.primaryDanceId).toBeUndefined();
  });

  it("resolves the marked item's id as the primary dance", () => {
    const q = new DanceQuery("ATN,BBA*");
    expect(q.primaryDanceId).toEqual("BBA");
  });

  it("ignores a primary marker on a dance group", () => {
    // WLZ is a dance group - groups have no per-dance rating/tempo fields of their own,
    // so a marker on one is not a valid scope target.
    const q = new DanceQuery("WLZ*,BOL");
    expect(q.primaryDanceId).toBeUndefined();
  });

  it("takes the first marker when more than one item is marked", () => {
    const q = new DanceQuery("ATN*,BBA*");
    expect(q.primaryDanceId).toEqual("ATN");
  });

  it("resolves a marked group's explicit member target", () => {
    // LTN (Latin) is a dance group containing CHA - marking the group with an explicit
    // target lets the scope chooser point at a member dance without that member ever
    // being a separately selected top-level item.
    const q = new DanceQuery("LTN*CHA");
    expect(q.primaryDanceId).toEqual("CHA");
  });

  it("ignores a marked group's target when it isn't one of the group's members", () => {
    const q = new DanceQuery("LTN*WCS");
    expect(q.primaryDanceId).toBeUndefined();
  });

  it("resolves a differently-cased group target to the canonical member id", () => {
    // A differently-cased target should still match, and resolve to the canonical (indexed)
    // casing - not the raw string from the filter - since downstream OData field paths like
    // dance_{id}/Votes must match the indexed field name exactly.
    const q = new DanceQuery("LTN*cha");
    expect(q.primaryDanceId).toEqual("CHA");
  });

  it("takes a valid group target over a later plain marker", () => {
    const q = new DanceQuery("LTN*CHA,RMB*");
    expect(q.primaryDanceId).toEqual("CHA");
  });
});
