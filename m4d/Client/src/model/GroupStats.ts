import { wordsToKebab } from "@/helpers/StringHelpers";
import { Tag } from "./Tag";
import { TagList } from "./TagList";
import { DanceStats } from "./DanceStats";
import { TypeStats } from "./TypeStats";
import { DanceGroup } from "./DanceGroup";

export class GroupStats extends DanceGroup implements DanceStats {
  public songCount: number;
  public maxWeight: number;
  public songTags: string;
  public dances: TypeStats[];

  constructor(base: DanceGroup, dances: TypeStats[]) {
    super(base);
    this.dances = this.danceIds.map((id) => dances.find((d) => d.id === id)!);
    this.songCount = this.dances.reduce(
      (acc, dance) => acc + dance.songCount,
      0
    );
    this.maxWeight = this.dances.reduce(
      (acc, dance) => Math.max(acc + dance.maxWeight),
      0
    );
    this.songTags = TagList.build(
      this.dances.reduce(
        (acc: Tag[], dance) => TagList.concat(acc, dance.tags),
        []
      )
    ).summary!;
    console.log(this.songTags);
  }

  public get isGroup(): boolean {
    return true;
  }

  public get seoName(): string {
    return wordsToKebab(this.name);
  }

  public get tags(): Tag[] {
    return new TagList(this.songTags).tags;
  }
}
