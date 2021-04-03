import axios from "axios";
import { TypedJSON } from "typedjson";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { TagGroup } from "@/model/TagGroup";

declare global {
  interface Window {
    environment?: DanceEnvironment;
  }
}

export async function getEnvironment(): Promise<DanceEnvironment> {
  if (window.environment) {
    return window.environment;
  }

  window.environment = loadFromStorage();
  if (window.environment) {
    return window.environment;
  }

  return loadStats();
}

async function loadStats(): Promise<DanceEnvironment> {
  try {
    const response = await axios.get("/api/dancesstatistics/");
    const data = response.data;
    sessionStorage.setItem("dance-stats", JSON.stringify(data));
    window.environment = TypedJSON.parse(data, DanceEnvironment);
    return window.environment!;
  } catch (e) {
    console.log(e);
    throw e;
  }
}

function loadFromStorage(): DanceEnvironment | undefined {
  const statString = sessionStorage.getItem("dance-stats");

  if (!statString) {
    return;
  }

  const environment = TypedJSON.parse(statString, DanceEnvironment);
  const incrementalString = sessionStorage.getItem("incremental-tags");
  if (incrementalString) {
    environment!.incrementalTags = TypedJSON.parseAsArray(
      incrementalString,
      TagGroup
    );
  }
  window.environment = environment;
  return environment;
}
