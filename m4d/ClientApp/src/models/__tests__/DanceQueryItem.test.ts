import { beforeAll, describe, expect, it } from "vitest";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import { loadTestDances } from "@/helpers/LoadTestDances";
import { loadDancesFromString } from "@/helpers/DanceLoader";
import { DanceDatabase } from "../DanceDatabase/DanceDatabase";
import { DanceQueryItem } from "../DanceQueryItem";

declare global {
  interface Window {
    danceDatabase: DanceDatabase | undefined;
  }
}

setupTestEnvironment();

describe("DanceQueryItem", () => {
  beforeAll(() => {
    window.danceDatabase = loadDancesFromString(loadTestDances());
  });

  it("parses id, threshold, and tags", () => {
    const item = DanceQueryItem.fromValue("BOL+2|Fast:Tempo|Smooth:Style");
    expect(item.id).toEqual("BOL");
    expect(item.threshold).toEqual(2);
    expect(item.primary).toBeFalsy();
    expect(item.tagQuery?.hasTags).toBe(true);
  });

  it("parses a negative threshold", () => {
    const item = DanceQueryItem.fromValue("RMB-3|Fun:Other");
    expect(item.id).toEqual("RMB");
    expect(item.threshold).toEqual(-3);
  });

  it("defaults primary to falsy when no marker is present", () => {
    const item = DanceQueryItem.fromValue("CHA+2");
    expect(item.primary).toBeFalsy();
  });

  it("parses the primary marker", () => {
    const item = DanceQueryItem.fromValue("CHA*+2|Fast:Tempo");
    expect(item.id).toEqual("CHA");
    expect(item.primary).toBe(true);
    expect(item.threshold).toEqual(2);
    expect(item.tagQuery?.hasTags).toBe(true);
  });

  it("round-trips the primary marker through toString", () => {
    const item = DanceQueryItem.fromValue("CHA*-3|Fast:Tempo");
    expect(item.toString()).toEqual("CHA*-3|Fast:Tempo");

    const reparsed = DanceQueryItem.fromValue(item.toString());
    expect(reparsed.primary).toBe(true);
    expect(reparsed.threshold).toEqual(-3);
  });

  it("places the primary marker before the threshold sign with no tags", () => {
    const item = DanceQueryItem.fromValue("CHA*");
    expect(item.toString()).toEqual("CHA*");
  });

  it("throws on an invalid format", () => {
    expect(() => DanceQueryItem.fromValue("!invalid")).toThrow();
  });
});
