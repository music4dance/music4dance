import { describe, expect, test } from "vitest";
import { DanceGroup } from "../DanceGroup";
import { DanceDatabase } from "../DanceDatabase";
import { loadDatabase } from "@/helpers/TestDatabase";
import { loadDancesFromString } from "@/helpers/DanceLoader";
import { loadTestDances } from "@/helpers/LoadTestDances";
import { DanceFilter } from "../DanceFilter";
import { DanceType } from "../DanceType";
import { TempoRange } from "../TempoRange";
import { Meter } from "../Meter";

describe("DanceDatabase.ts", () => {
  test("Loads a simple DanceDatabase", () => {
    const danceDb = loadDatabase();
    expect(danceDb).toBeDefined();
    expect(danceDb).toBeInstanceOf(DanceDatabase);
    expect(danceDb.all).toBeDefined();
    expect(danceDb.all).toBeInstanceOf(Array);
    expect(danceDb.all.length).toBe(3);
    expect(danceDb.dances).toBeDefined();
    expect(danceDb.dances).toBeInstanceOf(Array);
    expect(danceDb.dances.length).toBe(2);
    expect(danceDb.groups).toBeDefined();
    expect(danceDb.groups).toBeInstanceOf(Array);
    expect(danceDb.groups.length).toBe(1);
  });

  test("Groups are populated", () => {
    const danceDb = loadDatabase();
    const group = danceDb.groups[0];
    expect(group).toBeDefined();
    expect(group!.dances).toBeDefined();
    expect(group!.dances).toBeInstanceOf(Array);
    expect(group!.dances.length).toBe(2);
    const swz = group!.dances.find((d) => d.id === "SWZ");
    expect(swz).toBeDefined();
  });

  test("isGroup returns true for groups", () => {
    const group = loadDatabase().groups[0];
    expect(group).toBeDefined();
    expect(DanceGroup.isGroup(group!)).toBe(true);
  });

  test("isGroup returns false for types", () => {
    const dance = loadDatabase().dances[0];
    expect(dance).toBeDefined();
    expect(DanceGroup.isGroup(dance!)).toBe(false);
  });

  test("style returns all styles for types", () => {
    const styles = loadDatabase().styles;
    expect(styles).toBeDefined();
    expect(styles.length).toBe(2);
    expect(styles).toContain("American Smooth");
    expect(styles).toContain("International Standard");
  });

  test("style returns all styles for types in full DB", () => {
    const fullDB = loadDancesFromString(loadTestDances());
    const groups = fullDB.groups.map((g) => g.name).filter((n) => n !== "Performance");
    const db = fullDB.filter(new DanceFilter({ groups: groups }));
    const styles = db.styles;

    expect(styles).toBeDefined();
    expect(styles.length).toBe(6);
  });

  test("getStyleFamilies returns correct families for a dance", () => {
    const fullDB = loadDancesFromString(loadTestDances());
    const rumbas = fullDB.getStyleFamilies("RMB");
    expect(rumbas).toContain("American");
    expect(rumbas).toContain("International");
    expect(rumbas.length).toBe(2);
  });

  test("getStyleFamilies returns single family for dance with one style", () => {
    const fullDB = loadDancesFromString(loadTestDances());
    const quickstep = fullDB.getStyleFamilies("QST");
    expect(quickstep).toEqual(["International"]);
  });

  test("getStyleFamilies returns empty array for unknown dance", () => {
    const db = loadDatabase();
    const unknown = db.getStyleFamilies("XXX");
    expect(unknown).toEqual([]);
  });

  describe("filterTempo", () => {
    const dance = new DanceType({
      name: "test-dance",
      tempoRange: new TempoRange(100, 120),
      meter: new Meter(4, 4),
    });

    test("an epsilon of 0 keeps a dance whose range contains the tempo", () => {
      expect(DanceDatabase.filterTempo([dance], 100, 0)).toHaveLength(1);
      expect(DanceDatabase.filterTempo([dance], 110, 0)).toHaveLength(1);
      expect(DanceDatabase.filterTempo([dance], 120, 0)).toHaveLength(1);
    });

    test("an epsilon of 0 drops a dance whose range doesn't contain the tempo", () => {
      expect(DanceDatabase.filterTempo([dance], 99, 0)).toHaveLength(0);
      expect(DanceDatabase.filterTempo([dance], 121, 0)).toHaveLength(0);
    });

    test("keeps a dance exactly epsilon percent outside its range", () => {
      // 95 is 5% below 100; 126 is 5% above 120.
      expect(DanceDatabase.filterTempo([dance], 95, 5)).toHaveLength(1);
      expect(DanceDatabase.filterTempo([dance], 126, 5)).toHaveLength(1);
      expect(DanceDatabase.filterTempo([dance], 94, 5)).toHaveLength(0);
    });
  });
});
