import { describe, expect, test } from "vitest";
import { DanceInstance } from "../DanceInstance";
import { DanceException } from "../DanceException";
import { DanceType } from "../DanceType";
import { TempoRange } from "../TempoRange";
import { Meter } from "../Meter";

function buildDance(exceptions?: DanceException[]): DanceType {
  return new DanceType({
    internalId: "SWZ",
    internalName: "Slow Waltz",
    meter: new Meter(3, 4),
    organizations: ["DanceSport", "NDCA"],
    instances: [
      new DanceInstance({
        style: "American Smooth",
        tempoRange: new TempoRange(84.0, 90.0),
        competitionGroup: "Ballroom",
        exceptions: exceptions ?? [],
      }),
    ],
  });
}

describe("DanceType.ts", () => {
  test.skip("toJson serializes correctly", () => {
    const dance = buildDance();
    //const json = (dance as unknown as any).toJSON();
    const json = JSON.stringify(dance);
    console.log(json);
  });

  test("seo-name is correct", () => {
    const dance = buildDance();
    expect(dance.seoName).toBe("slow-waltz");
  });

  test("styles is correct", () => {
    const dance = buildDance();
    expect(dance.styles).toEqual(["American Smooth"]);
  });

  test("competitionDances is correct", () => {
    const dance = buildDance();
    expect(dance.competitionDances.length).toBe(1);
  });

  // INT-TODO: Add tests for filtering once I figure out which direction I want to go with that
});
