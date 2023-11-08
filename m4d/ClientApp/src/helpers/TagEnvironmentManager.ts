import { TagDatabase } from "@/models/TagDatabase";
import { loadTagsFromString } from "./TagLoader";

declare global {
  interface Window {
    tagDatabaseJson?: string;
  }
}

let tagDatabase: TagDatabase | undefined;

export function safeTagDatabase(): TagDatabase {
  const tags = syncTags();
  if (tags) {
    return tags;
  }
  throw new Error("Tag Database not loaded as expected");
}

function syncTags(): TagDatabase | undefined {
  if (tagDatabase) {
    return tagDatabase;
  }
  if (window.tagDatabaseJson) {
    tagDatabase = loadTagsFromString(window.tagDatabaseJson, true);
    return tagDatabase;
  }
  return undefined;
}
