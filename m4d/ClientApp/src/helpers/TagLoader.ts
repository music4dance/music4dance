import { Tag } from "@/models/Tag";
import { TagDatabase } from "@/models/TagDatabase";
import { TypedJSON } from "typedjson";

export function loadTagsFromString(s: string, includeIncremental?: boolean): TagDatabase {
  const tags = TypedJSON.parseAsArray(s, Tag);
  return new TagDatabase(tags, includeIncremental ? loadIncrementalTags() : undefined);
}
function loadIncrementalTags(): Tag[] | undefined {
  const incrementalString = sessionStorage.getItem("incremental-tags");
  return incrementalString ? TypedJSON.parseAsArray(incrementalString, Tag) : undefined;
}
