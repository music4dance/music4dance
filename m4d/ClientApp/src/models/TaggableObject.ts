import { jsonArrayMember, jsonObject } from "typedjson";
import { Tag } from "./Tag";
import { TagList } from "./TagList";
import { UserComment } from "./UserComment";

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
  @jsonArrayMember(UserComment) public comments: UserComment[] = [];

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
    return userTags ? !!userTags.find((t) => t.key.toLowerCase() === tag.key.toLowerCase()) : false;
  }

  public addComment(comment: string, userName: string): void {
    this.removeComment(userName);
    this.comments.push(new UserComment({ userName: userName, comment: comment }));
  }

  public removeComment(userName: string): void {
    this.comments = this.comments.filter((c) => c.userName != userName);
  }

  private static add(initial: Tag[], more: string): Tag[] {
    return TaggableObject.modify(initial, more, 1);
  }

  private static remove(initial: Tag[], less: string): Tag[] {
    return TaggableObject.modify(initial, less, -1);
  }

  private static modify(initial: Tag[], modified: string, direction: number): Tag[] {
    return this.merge(initial, new TagList(modified).tags, direction);
  }

  private static merge(a: Tag[], b: Tag[], direction: number): Tag[] {
    const map = new Map<string, Tag>(a.map((t) => [t.key, t]));
    b.forEach((tB) => {
      const key = tB.key;
      const tA = map.get(key);
      if (tA) {
        tA.count = (tA.count ?? 0) + direction;
      } else if (direction > 0) {
        map.set(tB.key, Tag.fromKey(key, 1));
      }
    });
    return [...map.values()].filter((t) => t.count! > 0).sort((a, b) => a.key.localeCompare(b.key));
  }
}
