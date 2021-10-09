import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { TagGroup } from "@/model/TagGroup";
import axios from "axios";
import { TypedJSON } from "typedjson";

declare global {
  interface Window {
    environment?: DanceEnvironment;
  }
}

const environmentKey = "dance-environmnet";
const expiryKey = "environment-expiry";

export async function getEnvironment(): Promise<DanceEnvironment> {
  if (window.environment) {
    return window.environment;
  }

  if (checkExpiry()) {
    window.environment = loadFromStorage();
  }
  if (window.environment) {
    return window.environment;
  }

  return loadStats();
}

async function loadStats(): Promise<DanceEnvironment> {
  try {
    const response = await axios.get("/api/danceenvironment/");
    const data = response.data;
    sessionStorage.setItem(environmentKey, JSON.stringify(data));
    sessionStorage.setItem(
      expiryKey,
      JSON.stringify(Date.now() + 1000 * 60 * 60)
    );
    window.environment = TypedJSON.parse(data, DanceEnvironment);
    return window.environment!;
  } catch (e) {
    console.log(e);
    throw e;
  }
}

function checkExpiry(): boolean {
  try {
    const expiryString = sessionStorage.getItem(expiryKey);
    if (!expiryString) {
      return false;
    }
    const expiry = JSON.parse(expiryString) as number;
    return Date.now() < expiry;
  } catch {
    return false;
  }
}

function loadFromStorage(): DanceEnvironment | undefined {
  const envString = sessionStorage.getItem(environmentKey);

  if (!envString) {
    return;
  }

  const environment = TypedJSON.parse(envString, DanceEnvironment);
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
