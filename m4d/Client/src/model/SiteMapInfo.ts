import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";
import { Link } from "./Link";

@jsonObject
export class SiteMapEntry {
  @jsonMember public title!: string;
  @jsonMember public reference!: string;
  @jsonMember public description!: string;
  @jsonMember public oneTime!: boolean;

  public get link(): Link {
    return { text: this.title, link: this.fullPath };
  }

  public get fullPath(): string {
    const blogPrefix: string = "blog/";
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
