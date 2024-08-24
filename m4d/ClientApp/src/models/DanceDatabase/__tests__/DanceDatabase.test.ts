import { describe, expect, test } from "vitest";
import { DanceGroup } from "../DanceGroup";
import { DanceDatabase } from "../DanceDatabase";
import { loadDatabase } from "@/helpers/TestDatabase";
import { loadDancesFromString } from "@/helpers/DanceLoader";
import { loadTestDances } from "@/helpers/LoadTestDances";
import { DanceFilter } from "../DanceFilter";

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
    expect(group.dances).toBeDefined();
    expect(group.dances).toBeInstanceOf(Array);
    expect(group.dances.length).toBe(2);
    const swz = group.dances.find((d) => d.id === "SWZ");
    expect(swz).toBeDefined();
  });

  test("isGroup returns true for groups", () => {
    const group = loadDatabase().groups[0];
    expect(DanceGroup.isGroup(group)).toBe(true);
  });

  test("isGroup returns false for types", () => {
    const group = loadDatabase().dances[0];
    expect(DanceGroup.isGroup(group)).toBe(false);
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
    expect(styles.length).toBe(5);
  });
});
