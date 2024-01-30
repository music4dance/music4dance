import { describe, expect, test } from "vitest";
import { DanceInstance } from "../DanceInstance";
import { DanceException } from "../DanceException";
import { DanceType } from "../DanceType";
import { TempoRange } from "../TempoRange";
import { Meter } from "../Meter";

function buildInstance(exceptions?: DanceException[]): DanceInstance {
  const inst = new DanceInstance({
    style: "American Smooth",
    tempoRange: new TempoRange(84.0, 90.0),
    exceptions: exceptions ?? [],
  });

  new DanceType({
    internalId: "SWZ",
    internalName: "Slow Waltz",
    meter: new Meter(3, 4),
    organizations: ["DanceSport", "NDCA"],
    instances: [inst],
  });

  return inst;
}

describe("DanceInstance.ts", () => {
  test("shortStyle is correct", () => {
    const inst = buildInstance();
    expect(inst.shortStyle).toBe("American");
  });

  test("styleId is correct", () => {
    const inst = buildInstance();
    expect(inst.styleId).toBe("A");
  });

  test("Computed name is correct", () => {
    const inst = buildInstance();
    expect(inst.name).toBe("American Slow Waltz");
  });

  test("Computed id is correct", () => {
    const inst = buildInstance();
    expect(inst.id).toBe("SWZA");
  });

  test("Filtered tempo is correct with no DanceExceptions", () => {
    const inst = buildInstance();
    expect(inst.filteredTempo(["American"])).toEqual(new TempoRange(84.0, 90.0));
  });

  test("Filtered tempo is correct with no Organizations", () => {
    const inst = buildInstance([
      new DanceException({ organization: "NDCA", tempoRange: new TempoRange(90.0, 94.0) }),
    ]);
    expect(inst.filteredTempo([])).toEqual(new TempoRange(84.0, 90.0));
  });

  test("Filtered tempo is correct with an Organization", () => {
    const inst = buildInstance([
      new DanceException({ organization: "NDCA", tempoRange: new TempoRange(90.0, 94.0) }),
    ]);
    expect(inst.filteredTempo(["NDCA"])).toEqual(new TempoRange(90.0, 94.0));
  });
});
