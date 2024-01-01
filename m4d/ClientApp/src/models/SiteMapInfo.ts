import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { type Link } from "./Link";

@jsonObject
export class SiteMapEntry {
  @jsonMember(String) public title!: string;
  @jsonMember(String) public reference!: string;
  @jsonMember(String) public description!: string;
  @jsonMember(Boolean) public oneTime?: boolean;
  @jsonMember(Number) public order?: number;
  @jsonArrayMember(SiteMapEntry) public children?: SiteMapEntry[];

  public get link(): Link {
    return { text: this.title, link: this.fullPath };
  }

  public get fullPath(): string {
    const blogPrefix = "blog/";
    const rel = this.reference;
    if (!rel) {
      return "";
    }
    if (rel === "blog") {
      return "https://music4dance.blog/";
    }
    if (rel.startsWith(blogPrefix)) {
      return `https://music4dance.blog/${rel.substring(blogPrefix.length)}`;
    }
    return `https://www.music4dance.net/${rel}`;
  }
}
