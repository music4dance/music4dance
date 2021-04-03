import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";
import { Tag } from "./Tag";

@jsonObject
export class TagGroup {
  public static ToTags(groups: TagGroup[]): Tag[] {
    return groups.map((g) => g.tag);
  }

  @jsonMember public key!: string;
  @jsonMember public modified?: Date;
  @jsonMember public count?: number;
  @jsonMember public primaryId?: string;

  public constructor(init?: Partial<TagGroup>) {
    Object.assign(this, init);
  }

  public get value(): string {
    const parts = this.key.split(":");
    return parts[0];
  }

  public get category(): string {
    const parts = this.key.split(":");
    return parts[1];
  }

  public get tag(): Tag {
    return new Tag({
      value: this.value,
      category: this.category,
      count: this.count ?? 0,
      primaryId: this.primaryId,
    });
  }
}
