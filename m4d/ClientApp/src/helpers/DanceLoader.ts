import { DanceDatabase } from "@/models/DanceDatabase";

export function loadDancesFromString(s: string): DanceDatabase {
  const db = DanceDatabase.load(s);
  if (!db) {
    throw new Error("Dance Database not loaded as expected");
  }
  return db;
}
