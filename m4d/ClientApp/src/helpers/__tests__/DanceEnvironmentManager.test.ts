import { DanceDatabase } from "@/models/DanceDatabase";
import { getDanceJson } from "../TestHelpers";
import { describe, expect, test } from "vitest";

describe("DanceDatabase.ts", () => {
  test("loads dances", () => {
    const json = getDanceJson();
    expect(json).toBeDefined();
    const database = DanceDatabase.load(json);
    expect(database).toBeDefined();
    expect(database).toBeInstanceOf(DanceDatabase);
    expect(database.dances).toBeDefined();
    expect(database.dances.length).toBeGreaterThan(0);
    expect(database.groups).toBeDefined();
    expect(database.groups.length).toBeGreaterThan(0);
  });
});
