import { DanceEnvironment } from "@/model/DanceEnvironment";
import { Tag } from "@/model/Tag";
import { TagDatabase } from "@/model/TagDatabase";
import axios from "axios";
import { TypedJSON } from "typedjson";

declare global {
  interface Window {
    environmentJson?: string;
    environment?: DanceEnvironment;
    tagDatabaseJson?: string;
    tagDatabase?: TagDatabase;
  }
}

const environmentKey = "dance-environmnet";
const tagDatabaseKey = "tag-database";
const expiryKey = "expiry";

export function safeEnvironment(): DanceEnvironment {
  if (window.environment) {
    return window.environment;
  }
  throw new Error("Dance Environment not loaded as expected");
}

export async function getEnvironment(): Promise<DanceEnvironment> {
  if (window.environment) {
    return window.environment;
  }

  if (window.environmentJson && loadDancesFromString(window.environmentJson)) {
    return window.environment!;
  }

  if (checkExpiry(environmentKey) && loadDancesFromStorage()) {
    return window.environment!;
  }

  return loadDances();
}

export function safeTagDatabase(): TagDatabase {
  if (window.tagDatabase) {
    return window.tagDatabase;
  }
  throw new Error("Tag Database not loaded as expected");
}

export async function getTagDatabase(): Promise<TagDatabase> {
  if (window.tagDatabase) {
    return window.tagDatabase;
  }

  if (window.tagDatabaseJson && loadTagsFromString(window.tagDatabaseJson)) {
    return window.tagDatabase!;
  }

  if (checkExpiry(tagDatabaseKey) && loadTagsFromStorage()) {
    return window.tagDatabase!;
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
    // eslint-disable-next-line no-console
    console.log(e);
    throw e;
  }
}

function loadDancesFromStorage(): boolean {
  const envString = sessionStorage.getItem(environmentKey);

  return envString ? loadDancesFromString(envString) : false;
}

function loadDancesFromString(s: string): boolean {
  window.environment = TypedJSON.parse(s, DanceEnvironment);
  return !!window.environment;
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
    // eslint-disable-next-line no-console
    console.log(e);
    throw e;
  }
}

function loadTagsFromStorage(): boolean {
  const tagsString = sessionStorage.getItem(tagDatabaseKey);

  if (!tagsString) {
    return false;
  }

  const tags = TypedJSON.parseAsArray(tagsString, Tag);
  window.tagDatabase = new TagDatabase(tags, loadIncrementalTags());
  return !!window.tagDatabase;
}

function loadTagsFromString(s: string): boolean {
  const tags = TypedJSON.parseAsArray(s, Tag);
  window.tagDatabase = new TagDatabase(tags, loadIncrementalTags());
  return !!window.tagDatabase;
}

function loadIncrementalTags(): Tag[] | undefined {
  const incrementalString = sessionStorage.getItem("incremental-tags");
  return incrementalString
    ? TypedJSON.parseAsArray(incrementalString, Tag)
    : undefined;
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
