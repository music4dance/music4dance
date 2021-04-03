import { TaggableObject } from "./TaggableObject";
import { SongFilter } from "./SongFilter";
import { Tag } from "./Tag";

export class TagHandler {
  constructor(
    public tag: Tag,
    public user?: string,
    public filter?: SongFilter,
    public parent?: TaggableObject
  ) {}

  public get id(): string {
    const parent = this.parent;
    return parent ? `${parent.id}-${this.tag.key}` : this.tag.key;
  }

  public get isSelected(): boolean {
    const parent = this.parent;
    return parent ? parent.isUserTag(this.tag) : false;
  }
}
