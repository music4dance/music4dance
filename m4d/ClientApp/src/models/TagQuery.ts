import { TagList } from "./TagList";

export class TagQuery {
  public tagList: TagList;

  constructor(tagString?: string) {
    this.tagList = new TagList(tagString ?? "");
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

  public get describeTags(): string {
    const inc = this.tagList.filterCategories(["Dances"]).AddsDescription;
    const exc = this.tagList.filterCategories(["Dances"]).RemovesDescription;
    return [inc, exc].filter(Boolean).join(" ");
  }
}
