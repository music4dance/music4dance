import { jsonMember, jsonObject } from "typedjson";
import type { DanceDatabase } from "./DanceDatabase";
import { Tag, TagCategory } from "./Tag";
import { TaggableObject } from "./TaggableObject";
import type { NamedObject } from "./NamedObject";

declare const danceDatabase: DanceDatabase;

@jsonObject
export class DanceRating extends TaggableObject {
  @jsonMember(String) public danceId!: string;
  @jsonMember(Number) public weight!: number;

  public static fromTag(tag: Tag): DanceRating {
    if (tag.category !== TagCategory.Dance) {
      throw new Error(`Can't create a dancerating form a non-dance tag ${tag.key}`);
    }
    const decorated = tag.value.match(/^[!\-+].*/);
    const value = decorated ? tag.value.substr(1) : tag.value;
    const dance = danceDatabase.fromName(value);
    if (!dance) {
      throw new Error(`Couldn't find dance ${value}`);
    }
    return new DanceRating({
      danceId: dance.id,
    });
  }

  public constructor(init?: Partial<DanceRating>) {
    super();
    Object.assign(this, init);
  }

  public get id(): string {
    return this.danceId;
  }

  public get categories(): string[] {
    return ["Style", "Tempo", "Other"];
  }

  public get modifier(): string {
    return `:${this.danceId}`;
  }

  public get positiveTag(): Tag {
    return Tag.fromParts(this.dance.name, TagCategory.Dance);
  }

  public get negativeTag(): Tag {
    return Tag.fromParts("!" + this.dance.name, TagCategory.Dance);
  }

  public get description(): string {
    return danceDatabase.fromId(this.danceId)!.name;
  }

  public get dance(): NamedObject {
    return danceDatabase.fromId(this.danceId)!;
  }
}
