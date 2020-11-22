import "reflect-metadata";
import { jsonMember, jsonObject, TypedJSON, jsonArrayMember } from "typedjson";
import { DanceInstance, DanceStats, DanceType } from "./DanceStats";
import { Tag } from "./Tag";

TypedJSON.setGlobalConfig({
  errorHandler: (e) => {
    console.error(e);
    throw e;
  },
});

@jsonObject
class TagGroup {
  public static ToTags(groups: TagGroup[]): Tag[] {
    return groups.map((g) => g.tag);
  }

  @jsonMember public key!: string;
  @jsonMember public modified!: Date;
  @jsonMember public count?: number;
  @jsonMember public primaryId?: string;

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

@jsonObject
export class DanceEnvironment {
  @jsonArrayMember(DanceStats, { name: "tree" }) public stats?: DanceStats[];
  @jsonArrayMember(TagGroup, { name: "TagGroups" })
  public tagGroups?: TagGroup[];

  private tagCache?: Tag[];

  public get tags(): Tag[] | undefined {
    if (!this.tagCache && this.tagGroups) {
      this.tagCache = TagGroup.ToTags(this.tagGroups);
    }
    return this.tagCache;
  }

  public fromId(id: string): DanceStats | undefined {
    return this.flatStats.find((d) => id === d.danceId);
  }

  public fromName(name: string): DanceStats | undefined {
    const n = name.toLowerCase();
    return this.flatStats.find((d) => n === d.danceName.toLowerCase());
  }

  public get flatStats(): DanceStats[] {
    return this.stats!.flatMap((group) => [group, ...group.children]).filter(
      (s) => s
    );
  }

  public get flatTypes(): DanceType[] {
    return this.flatStats
      .flatMap((group) => group.children)
      .filter((ds) => ds && ds.danceType)
      .map((ds) => ds.danceType!);
  }

  public get flatInstances(): DanceInstance[] {
    return this.flatTypes.flatMap((d) => d && d.instances);
  }

  public get groupedStats(): DanceStats[] {
    return this.stats!.sort((a, b) =>
      a.danceName.localeCompare(b.danceName)
    ).flatMap((group) => [
      group,
      ...group.children.sort((a, b) => a.danceName.localeCompare(b.danceName)),
    ]);
  }

  public get styles(): string[] {
    const styles = this.flatInstances.map((inst) => inst && inst.style);
    return [...new Set(styles)].sort();
  }

  public get types(): string[] {
    return this.stats!.map((s) => s.danceName);
  }
}
