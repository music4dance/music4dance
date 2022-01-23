import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";
import { DanceEnvironment } from "./DanceEnvironmet";
import { DanceStats } from "./DanceStats";
import { Tag, TagCategory } from "./Tag";
import { TaggableObject } from "./TaggableObject";

declare const environment: DanceEnvironment;

@jsonObject
export class DanceRating extends TaggableObject {
  @jsonMember public danceId!: string;
  @jsonMember public weight!: number;

  public static fromTag(tag: Tag): DanceRating {
    if (tag.category !== TagCategory.Dance) {
      throw new Error(
        `Can't create a dancerating form a non-dance tag ${tag.key}`
      );
    }
    const decorated = tag.value.match(/^[!\-+].*/);
    const value = decorated ? tag.value.substr(1) : tag.value;
    const dance = environment.fromName(value);
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
    return ["Style", "Tempo", "Other", "Music"];
  }

  public get modifier(): string {
    return `:${this.danceId}`;
  }

  public get positiveTag(): Tag {
    return Tag.fromParts(this.stats.name, TagCategory.Dance);
  }

  public get negativeTag(): Tag {
    return Tag.fromParts("!" + this.stats.name, TagCategory.Dance);
  }

  public get description(): string {
    return environment.fromId(this.danceId)!.name;
  }

  public get stats(): DanceStats {
    return environment.fromId(this.danceId)!;
  }
}
