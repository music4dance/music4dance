import { TagList } from "./TagList";
import { Tag } from "./Tag";

export class TagQuery {
  public includeDancesAllInSongTags: boolean;
  private data: string;

  public static fromParts(tagList: TagList, includeDancesAll: boolean = false): TagQuery {
    // Use TagList's summary property which is the stringified version
    let tagString = tagList.summary;

    if (includeDancesAll) {
      tagString = "^" + tagString;
    }
    return new TagQuery(tagString);
  }

  constructor(tagString?: string) {
    const originalString = tagString ?? "";
    this.data = originalString;

    if (originalString.startsWith("^")) {
      this.includeDancesAllInSongTags = true;
    } else {
      this.includeDancesAllInSongTags = false;
    }
  }

  public get query(): string {
    return this.data;
  }

  public get tagList(): TagList {
    let s = this.data;
    if (s.startsWith("^")) {
      s = s.substring(1);
    }
    return new TagList(s);
  }

  public addTag(tagKey: string, include: boolean = true, includeDancesAll?: boolean): TagQuery {
    // Create the new tag
    const modifier = include ? "+" : "-";
    const newTagString = modifier + tagKey;
    const newTag = Tag.fromString(newTagString);

    // Add to existing tag list
    const updatedTagList = this.tagList.add(newTag);

    // Use provided includeDancesAll or fall back to existing flag
    const finalIncludeDancesAll = includeDancesAll ?? this.includeDancesAllInSongTags;

    // Return new TagQuery with the resolved includeDancesAll flag
    return TagQuery.fromParts(updatedTagList, finalIncludeDancesAll);
  }

  public static tagFromFacetId(facetId: string): string | undefined {
    if (!facetId) return undefined;
    const lastPart = facetId.includes("/")
      ? facetId.substring(facetId.lastIndexOf("/") + 1)
      : facetId;
    return TagQuery.tagFromClassName(
      lastPart.endsWith("Tags") ? lastPart.substring(0, lastPart.length - 4) : lastPart,
    );
  }

  public static tagFromClassName(tagClass: string): string | undefined {
    return tagClass?.toLowerCase() === "genre" ? "Music" : tagClass;
  }

  public get hasTags(): boolean {
    return this.tagList.tags.length > 0;
  }

  public get description(): string {
    const filteredTagList = this.tagList.filterCategories(["Dances"]);
    const inc = filteredTagList.AddsDescription;
    const exc = filteredTagList.RemovesDescription;

    // When includeDancesAllInSongTags is true, we're searching both song and dance tags
    if (this.includeDancesAllInSongTags && (inc || exc)) {
      // Replace "including tag" with "including song or dance tag"
      const modifiedInc = inc ? inc.replace("including tag", "including song or dance tag") : "";
      const modifiedExc = exc ? exc.replace("excluding tag", "excluding song or dance tag") : "";
      return [modifiedInc, modifiedExc].filter(Boolean).join(" ");
    }

    return [inc, exc].filter(Boolean).join(" ");
  }

  public get shortDescription(): string {
    const filteredTagList = this.tagList.filterCategories(["Dances"]);
    const inc = filteredTagList.AddsShortDescription;
    const exc = filteredTagList.RemovesShortDescription;

    // When includeDancesAllInSongTags is true, add "song+dance" prefix
    if (this.includeDancesAllInSongTags && (inc || exc)) {
      const prefix = "song+dance ";
      return [prefix + inc, exc].filter(Boolean).join(" ");
    }

    return [inc, exc].filter(Boolean).join(" ");
  }
}
