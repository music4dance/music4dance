import { TagList } from "./TagList";
import { Tag } from "./Tag";
export class TagQuery {
  public excludeDanceTags: boolean;
  private data: string;

  // excludeDanceTags: false means include dance_ALL tags (default), true means do NOT include dance_ALL tags (add ^ prefix)
  public static fromParts(tagList: TagList, excludeDanceTags: boolean = false): TagQuery {
    let tagString = tagList.summary;
    if (excludeDanceTags) {
      tagString = "^" + tagString;
    }
    return new TagQuery(tagString);
  }

  constructor(tagString?: string) {
    const originalString = tagString ?? "";
    this.data = originalString;
    // Now: ^ means exclude dance_ALL tags
    if (originalString.startsWith("^")) {
      this.excludeDanceTags = true;
    } else {
      this.excludeDanceTags = false;
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

  public addTag(tagKey: string, include: boolean = true, excludeDanceTags?: boolean): TagQuery {
    // Create the new tag
    const modifier = include ? "+" : "-";
    const newTagString = modifier + tagKey;
    const newTag = Tag.fromString(newTagString);

    // Add to existing tag list
    const updatedTagList = this.tagList.add(newTag);

    // Use provided excludeDanceTags or fall back to existing flag
    const finalExcludeDanceTags = excludeDanceTags ?? this.excludeDanceTags;
    // Return new TagQuery with the resolved excludeDanceTags flag
    return TagQuery.fromParts(updatedTagList, finalExcludeDanceTags);
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

    // When excludeDanceTags is true, we're only searching song tags
    if (this.excludeDanceTags && (inc || exc)) {
      const modifiedInc = inc ? inc.replace("including tag", "including song tag") : "";
      const modifiedExc = exc ? exc.replace("excluding tag", "excluding song tag") : "";
      return [modifiedInc, modifiedExc].filter(Boolean).join(" ");
    }
    return [inc, exc].filter(Boolean).join(" ");
  }

  public get shortDescription(): string {
    const filteredTagList = this.tagList.filterCategories(["Dances"]);
    const inc = filteredTagList.AddsShortDescription;
    const exc = filteredTagList.RemovesShortDescription;

    // When excludeDanceTags is true, add "song" prefix
    if (!this.excludeDanceTags && (inc || exc)) {
      const prefix = "song ";
      return [prefix + inc, exc].filter(Boolean).join(" ");
    }
    return [inc, exc].filter(Boolean).join(" ");
  }
}
