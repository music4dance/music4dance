import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { Tag } from "@/model/Tag";
import { TagDatabase } from "@/model/TagDatabase";
import axios from "axios";
import { TypedJSON } from "typedjson";

declare global {
  interface Window {
    environment?: DanceEnvironment;
    tagDatabase?: TagDatabase;
  }
}

const environmentKey = "dance-environmnet";
const tagDatabaseKey = "tag-database";
const expiryKey = "expiry";

export async function getEnvironment(): Promise<DanceEnvironment> {
  if (window.environment) {
    return window.environment;
  }

  if (checkExpiry(environmentKey)) {
    window.environment = loadDancesFromStorage();
  }

  if (window.environment) {
    return window.environment;
  }

  return loadDances();
}

export async function getTagDatabase(): Promise<TagDatabase> {
  if (window.tagDatabase) {
    return window.tagDatabase;
  }

  if (checkExpiry(tagDatabaseKey)) {
    window.tagDatabase = loadTagsFromStorage();
  }
  if (window.tagDatabase) {
    return window.tagDatabase;
  }

  return loadTags();
}

async function loadDances(): Promise<DanceEnvironment> {
  try {
    const response = await axios.get("/api/danceenvironment/");
    const data = response.data;
    sessionStorage.setItem(environmentKey, JSON.stringify(data));
    setExpiry(environmentKey);
    window.environment = TypedJSON.parse(data, DanceEnvironment);
    return window.environment!;
  } catch (e) {
    console.log(e);
    throw e;
  }
}

function loadDancesFromStorage(): DanceEnvironment | undefined {
  const envString = sessionStorage.getItem(environmentKey);

  if (!envString) {
    return;
  }

  window.environment = TypedJSON.parse(envString, DanceEnvironment);
  return window.environment;
}

async function loadTags(): Promise<TagDatabase> {
  try {
    const response = await axios.get("/api/tag/");
    const data = response.data;
    sessionStorage.setItem(tagDatabaseKey, JSON.stringify(data));
    setExpiry(tagDatabaseKey);
    const tags = TypedJSON.parseAsArray(data, Tag);
    window.tagDatabase = new TagDatabase(tags);
    return window.tagDatabase;
  } catch (e) {
    console.log(e);
    throw e;
  }
}

function loadTagsFromStorage(): TagDatabase | undefined {
  const tagsString = sessionStorage.getItem(tagDatabaseKey);

  if (!tagsString) {
    return;
  }

  const tags = TypedJSON.parseAsArray(tagsString, Tag);
  const incrementalString = sessionStorage.getItem("incremental-tags");
  let incrementalTags;
  if (incrementalString) {
    incrementalTags = TypedJSON.parseAsArray(incrementalString, Tag);
  }
  window.tagDatabase = new TagDatabase(tags, incrementalTags);
  return window.tagDatabase;
}

function checkExpiry(key: string): boolean {
  try {
    const expiryString = sessionStorage.getItem(`${key}-${expiryKey}`);
    if (!expiryString) {
      return false;
    }
    const expiry = JSON.parse(expiryString) as number;
    return Date.now() < expiry;
  } catch {
    return false;
  }
}

function setExpiry(key: string): void {
  sessionStorage.setItem(
    `${key}-${expiryKey}`,
    JSON.stringify(Date.now() + 1000 * 60 * 60)
  );
}
