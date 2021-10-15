import "reflect-metadata";
import { jsonArrayMember, jsonObject } from "typedjson";
import { DanceEnvironment } from "./DanceEnvironmet";
import { Tag } from "./Tag";
import { TagList } from "./TagList";

declare const environment: DanceEnvironment;

@jsonObject
export class TaggableObject {
  public get description(): string {
    throw new Error("Invalid call to abstract methos");
  }

  public get id(): string {
    throw new Error("Invalid call to abstract methos");
  }

  public get categories(): string[] {
    throw new Error("Invalid call to abstract methos");
  }

  public get modifier(): string {
    return "";
  }

  @jsonArrayMember(Tag) public tags: Tag[] = [];
  @jsonArrayMember(Tag) public currentUserTags: Tag[] = [];

  public addTags(tags: string, currentUser = false): void {
    this.tags = TaggableObject.add(this.tags, tags);
    if (currentUser) {
      this.currentUserTags = TaggableObject.add(this.currentUserTags, tags);
    }
  }

  public removeTags(tags: string, currentUser = false): void {
    this.tags = TaggableObject.remove(this.tags, tags);
    if (currentUser) {
      this.currentUserTags = TaggableObject.remove(this.currentUserTags, tags);
    }
  }

  public deleteTag(tag: Tag): void {
    this.tags = this.tags.filter((t) => t.key !== tag.key);
  }

  public isUserTag(tag: Tag): boolean {
    const userTags = this.currentUserTags;
    return userTags
      ? !!userTags.find((t) => t.key.toLowerCase() === tag.key.toLowerCase())
      : false;
  }

  private static add(initial: Tag[], more: string): Tag[] {
    return TaggableObject.modify(initial, more, 1);
  }

  private static remove(initial: Tag[], less: string): Tag[] {
    return TaggableObject.modify(initial, less, -1);
  }

  private static normalize(tags: string, count: number): Tag[] {
    return environment.tagDatabase.normalizeTagList(new TagList(tags), count)
      .tags;
  }

  private static modify(
    initial: Tag[],
    modified: string,
    direction: number
  ): Tag[] {
    const normal = TaggableObject.normalize(modified, direction);
    return this.merge(initial, normal);
  }

  private static merge(a: Tag[], b: Tag[]): Tag[] {
    const map = new Map<string, Tag>(a.map((t) => [t.key, t]));
    b.forEach((tB) => {
      const tA = map.get(tB.key);
      if (tA) {
        tA.count = (tA.count ?? 0) + (tB.count ?? 0);
      } else {
        map.set(tB.key, tB);
      }
    });
    return [...map.values()]
      .filter((t) => t.count! > 0)
      .sort((a, b) => a.key.localeCompare(b.key));
  }
}
