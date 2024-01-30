import { describe, expect, test } from "vitest";
import { loadTestDances } from "@/helpers/LoadTestDances";

import { DanceFilter } from "../DanceFilter";
import { loadDancesFromString } from "@/helpers/DanceLoader";
import { Meter } from "../Meter";

import _206Default from "./TestData/Filtered206Default.json";
import _206NoAmericanRhythm from "./TestData/Filtered206NoAmericanRhythm.json";
import _206JustAmericanRhythm from "./TestData/Filtered206JustAmericanRhythm.json";
import _126DanceSport from "./TestData/Filtered126DanceSport.json";
import _126NDCA from "./TestData/Filtered126NDCA.json";
import _waltz from "./TestData/FilteredWaltz.json";
const _nullDances: string[] = [];

describe("DanceDatabase.ts (filtering)", () => {
  test("206 BPM Default", () => {
    compareDanceOrder(new DanceFilter({ meters: [new Meter(4, 4)] }), 206, 10, _206Default);
  });

  test("206 BPM No American Rhythm", () => {
    compareDanceOrder(
      new DanceFilter({
        meters: [new Meter(4, 4)],
        styles: ["International Standard", "International Latin", "American Smooth", "Social"],
      }),
      206,
      10,
      _206NoAmericanRhythm,
    );
  });

  test("206 BPM Just American Rhythm", () => {
    compareDanceOrder(
      new DanceFilter({
        meters: [new Meter(4, 4)],
        styles: ["American Rhythm"],
      }),
      206,
      10,
      _206JustAmericanRhythm,
    );
  });

  test("126 BPM DanceSport", () => {
    compareDanceOrder(
      new DanceFilter({
        meters: [new Meter(4, 4)],
        organizations: ["DanceSport"],
      }),
      126,
      10,
      _126DanceSport,
    );
  });

  test("126 BPM NDCA", () => {
    compareDanceOrder(
      new DanceFilter({
        meters: [new Meter(4, 4)],
        organizations: ["NDCA"],
      }),
      126,
      10,
      _126NDCA,
    );
  });

  test("Null Dances", () => {
    compareDanceOrder(
      new DanceFilter({
        meters: [new Meter(4, 4)],
      }),
      500,
      20,
      _nullDances,
    );
  });

  test("Waltz", () => {
    compareDanceOrder(
      new DanceFilter({
        meters: [new Meter(3, 4)],
      }),
      94.5,
      20,
      _waltz,
    );
  });
});

function compareDanceOrder(
  filter: DanceFilter,
  tempo: number,
  epsilon: number,
  expected: string[],
): void {
  const danceDb = loadDancesFromString(loadTestDances());

  let succeeded = true;

  const dances = danceDb.filterDances(filter, tempo, epsilon);

  let i = 0;
  for (const dance of dances) {
    const s = dance.toString();

    if (expected != null) {
      if (i < expected.length) {
        const match = s === expected[i];
        if (!match) {
          console.log("");
        }

        succeeded = succeeded && match;
      }
    }

    console.log(`"${s}",`);

    i += 1;
  }

  if (expected != null) {
    expect(i).toEqual(expected.length);
  }

  console.log("------");

  expect(succeeded).toBe(true);
}
