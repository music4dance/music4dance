import { DanceDatabase } from "@/models/DanceDatabase";
import { loadDancesFromString } from "./DanceLoader";

declare global {
  interface Window {
    danceDatabaseJson?: string;
  }
}

let danceDatabase: DanceDatabase | undefined;

export function safeDanceDatabase(): DanceDatabase {
  if (danceDatabase) {
    return danceDatabase;
  }

  if (!window.danceDatabaseJson) {
    throw new Error("danceDatabaseJson not defined on window");
  }

  const db = loadDancesFromString(window.danceDatabaseJson);
  if (db) {
    danceDatabase = db;
    return db;
  }
  throw new Error("Dance Database not loaded as expected");
}
