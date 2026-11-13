import { beforeAll, describe, expect, it } from "vitest";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import { loadTestDances } from "@/helpers/LoadTestDances";
import { loadDancesFromString } from "@/helpers/DanceLoader";
import { DanceDatabase } from "../DanceDatabase/DanceDatabase";
import { DanceText } from "../DanceText";

declare global {
  interface Window {
    danceDatabase: DanceDatabase | undefined;
  }
}

setupTestEnvironment();

describe("DanceText", () => {
  beforeAll(() => {
    window.danceDatabase = loadDancesFromString(loadTestDances());
  });

  it("names the dance in the empty-description placeholder", () => {
    const text = new DanceText("").expanded("Slow Waltz");
    expect(text).toContain("the Slow Waltz");
    expect(text).not.toContain("@Model");
  });

  it("falls back to a generic placeholder without a dance name", () => {
    const text = new DanceText("").expanded();
    expect(text).toContain("this dance");
    expect(text).not.toContain("@Model");
  });

  it("links a bare dance reference", () => {
    expect(new DanceText("Try the [Slow Waltz] today.").expanded()).toBe(
      "Try the [Slow Waltz](/dances/slow-waltz) today.",
    );
  });

  it("leaves an existing link alone", () => {
    const description = "See [Slow Waltz](/somewhere).";
    expect(new DanceText(description).expanded()).toBe(description);
  });

  it("links a category reference", () => {
    expect(new DanceText("Part of [American Smooth] syllabus.").expanded()).toBe(
      "Part of [American Smooth](/dances/american-smooth) syllabus.",
    );
  });
});
