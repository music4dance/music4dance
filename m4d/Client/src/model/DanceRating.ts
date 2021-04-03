import { jsonMember, jsonObject } from "typedjson";
import { Tag } from "./Tag";
import { TaggableObject } from "./TaggableObject";
import { DanceStats } from "./DanceStats";
import { DanceEnvironment } from "./DanceEnvironmet";

declare const environment: DanceEnvironment;

@jsonObject
export class DanceRating extends TaggableObject {
  @jsonMember public danceId!: string;
  @jsonMember public weight!: number;

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
    return new Tag({ value: this.stats.danceName, category: "Dance" });
  }

  public get negativeTag(): Tag {
    return new Tag({ value: "!" + this.stats.danceName, category: "Dance" });
  }

  public get description(): string {
    return environment.fromId(this.danceId)!.danceName;
  }

  public get stats(): DanceStats {
    return environment.fromId(this.danceId)!;
  }
}
