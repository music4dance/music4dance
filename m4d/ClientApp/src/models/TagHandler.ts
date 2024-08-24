import { v4 as uuidv4 } from "uuid";
import { SongFilter } from "./SongFilter";
import { Tag } from "./Tag";
import { TaggableObject } from "./TaggableObject";

export class TagHandler {
  public id: string;

  constructor(
    public tag: Tag,
    public user?: string,
    public filter?: SongFilter,
    public parent?: TaggableObject,
    keyId?: boolean,
  ) {
    this.id = keyId ? tag.key : uuidv4();
  }

  public get isSelected(): boolean {
    const parent = this.parent;
    return parent ? parent.isUserTag(this.tag) : false;
  }
}
