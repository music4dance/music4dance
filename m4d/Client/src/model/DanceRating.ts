import { jsonMember, jsonObject } from "typedjson";
import { Tag, TagCategory } from "./Tag";
import { TaggableObject } from "./TaggableObject";
import { DanceStats } from "./DanceStats";
import { DanceEnvironment } from "./DanceEnvironmet";

declare const environment: DanceEnvironment;

@jsonObject
export class DanceRating extends TaggableObject {
  @jsonMember public danceId!: string;
  @jsonMember public weight!: number;

  public static fromTag(tag: Tag): DanceRating {
    if (tag.category !== TagCategory.Dance) {
      throw new Error("Can't creat a dancerating form a non-dance tag");
    }
    const positive = !tag.value.startsWith("!");
    const value = positive ? tag.value : tag.value.substr(1);
    return new DanceRating({
      danceId: environment.fromName(value)!.id,
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
    return new Tag({
      value: this.stats.name,
      category: TagCategory.Dance,
    });
  }

  public get negativeTag(): Tag {
    return new Tag({
      value: "!" + this.stats.name,
      category: TagCategory.Dance,
    });
  }

  public get description(): string {
    return environment.fromId(this.danceId)!.name;
  }

  public get stats(): DanceStats {
    return environment.fromId(this.danceId)!;
  }
}
