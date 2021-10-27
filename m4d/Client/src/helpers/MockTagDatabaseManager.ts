import { Tag } from "@/model/Tag";
import { TagDatabase } from "@/model/TagDatabase";
import { TypedJSON } from "typedjson";
import tagDatabaseJson from "../assets/tags.json";

declare global {
  interface Window {
    tagDatabe?: TagDatabase;
  }
}

export function getTagDatabaseMock(): TagDatabase {
  if (!window.tagDatabase) {
    const tags = TypedJSON.parseAsArray(tagDatabaseJson, Tag);
    window.tagDatabase = new TagDatabase(tags);
  }

  return window.tagDatabase!;
}
