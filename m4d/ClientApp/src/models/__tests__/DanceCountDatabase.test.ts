import { describe, expect, test } from "vitest";
import { DanceDatabase } from "@/models/DanceDatabase/DanceDatabase";
import { loadCountDatabase } from "@/helpers/TestDatabase";
import { DanceTypeCount } from "../DanceTypeCount";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";

describe("DanceCountDatabase.ts", () => {
  test("Loads a simple DanceCountDatabase", () => {
    const db = loadCountDatabase();
    expect(db).toBeDefined();
    expect(db).toBeInstanceOf(DanceDatabase);
    expect(db.all).toBeDefined();
    expect(db.all).toBeInstanceOf(Array);
    expect(db.all.length).toBe(3);
  });

  test("Counts to be correct", () => {
    const db = loadCountDatabase();

    const swz = db.all.find((d) => d.id === "SWZ");
    expect(swz).toBeDefined();
    expect(swz).toBeInstanceOf(DanceTypeCount);
    expect((swz as DanceTypeCount).count).toBe(37);

    const sft = db.all.find((d) => d.id === "SFT");
    expect(sft).toBeDefined();
    expect(sft).toBeInstanceOf(DanceTypeCount);
    expect((sft as DanceTypeCount).count).toBe(109);

    const group = db.all.find((d) => d.id === "FOO");
    expect(group).toBeDefined();
    expect(group).toBeInstanceOf(DanceGroup);
  });
});
