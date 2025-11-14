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

  test("styleFamilies returns unique style families", () => {
    const dance = new DanceType({
      internalId: "RMB",
      internalName: "Rumba",
      meter: new Meter(4, 4),
      instances: [
        new DanceInstance({
          style: "American Rhythm",
          tempoRange: new TempoRange(120.0, 124.0),
          competitionGroup: "Ballroom",
        }),
        new DanceInstance({
          style: "International Latin",
          tempoRange: new TempoRange(100.0, 108.0),
          competitionGroup: "Ballroom",
        }),
      ],
    });
    expect(dance.styleFamilies).toEqual(["American", "International"]);
  });

  test("styleFamilies handles single style", () => {
    const dance = buildDance();
    expect(dance.styleFamilies).toEqual(["American"]);
  });

  test("styleFamilies handles Social style", () => {
    const dance = new DanceType({
      internalId: "WCS",
      internalName: "West Coast Swing",
      meter: new Meter(4, 4),
      instances: [
        new DanceInstance({
          style: "Social",
          tempoRange: new TempoRange(80.0, 130.0),
        }),
      ],
    });
    expect(dance.styleFamilies).toEqual(["Social"]);
  });

  test("styleFamilies deduplicates styles from same family", () => {
    const dance = new DanceType({
      internalId: "SWZ",
      internalName: "Slow Waltz",
      meter: new Meter(3, 4),
      instances: [
        new DanceInstance({
          style: "American Smooth",
          tempoRange: new TempoRange(84.0, 90.0),
          competitionGroup: "Ballroom",
        }),
        new DanceInstance({
          style: "American Rhythm",
          tempoRange: new TempoRange(120.0, 124.0),
          competitionGroup: "Ballroom",
        }),
      ],
    });
    // Both instances have "American" family, should only appear once
    expect(dance.styleFamilies).toEqual(["American"]);
  });

  // INT-TODO: Add tests for filtering once I figure out which direction I want to go with that
});
