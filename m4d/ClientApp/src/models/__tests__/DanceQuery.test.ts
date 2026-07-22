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
});
