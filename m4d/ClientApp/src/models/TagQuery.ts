import { TagList } from "./TagList";

export class TagQuery {
  public tagList: TagList;
  public includeDancesAllInSongTags: boolean;

  constructor(tagString?: string) {
    let s = tagString ?? "";
    if (s.startsWith("^")) {
      this.includeDancesAllInSongTags = true;
      s = s.substring(1);
    } else {
      this.includeDancesAllInSongTags = false;
    }
    this.tagList = new TagList(s);
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
    const inc = this.tagList.filterCategories(["Dances"]).AddsDescription;
    const exc = this.tagList.filterCategories(["Dances"]).RemovesDescription;
    const prefix = this.includeDancesAllInSongTags ? "song and dance tags " : "";
    return [prefix, inc, exc].filter(Boolean).join(" ");
  }

  public get shortDescription(): string {
    const inc = this.tagList.filterCategories(["Dances"]).AddsShortDescription;
    const exc = this.tagList.filterCategories(["Dances"]).RemovesShortDescription;
    const prefix = this.includeDancesAllInSongTags ? "song+dance " : "";
    return [prefix, inc, exc].filter(Boolean).join(" ");
  }
}
