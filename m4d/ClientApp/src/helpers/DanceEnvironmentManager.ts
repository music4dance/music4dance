import { DanceDatabase } from "@/models/DanceDatabase/DanceDatabase";
import { loadDancesFromString } from "./DanceLoader";

declare global {
  interface Window {
    danceDatabaseJson?: string;
    danceDatabase: DanceDatabase | undefined;
  }
}

export function safeDanceDatabase(): DanceDatabase {
  if (window.danceDatabase) {
    return window.danceDatabase;
  }

  if (!window.danceDatabaseJson) {
    throw new Error("danceDatabaseJson not defined on window");
  }

  const db = loadDancesFromString(window.danceDatabaseJson);
  if (db) {
    window.danceDatabase = db;
    return db;
  }
  throw new Error("Dance Database not loaded as expected");
}
