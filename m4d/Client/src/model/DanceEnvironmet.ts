import "reflect-metadata";
import { jsonArrayMember, jsonObject, TypedJSON } from "typedjson";
import { DanceGroup } from "./DanceGroup";
import { DanceInstance } from "./DanceInstance";
import { DanceStats } from "./DanceStats";
import { GroupStats } from "./GroupStats";
import { NamedObject } from "./NamedObject";
import { TypeStats } from "./TypeStats";

TypedJSON.setGlobalConfig({
  errorHandler: (e) => {
    console.error(e);
    throw e;
  },
});

@jsonObject({ onDeserialized: "rehydrate" })
export class DanceEnvironment {
  @jsonArrayMember(TypeStats) public dances?: TypeStats[];
  @jsonArrayMember(DanceGroup) public groups?: DanceGroup[];

  public tree?: GroupStats[];
  public statsIdMap?: Map<string, DanceStats>;

  public rehydrate(): void {
    this.tree = this.groups?.map((g) => new GroupStats(g, this.dances!));
    this.statsIdMap = new Map<string, DanceStats>(
      this.dances?.map((d) => [d.id, d])
    );
    this.tree?.forEach((g) =>
      g.danceIds.forEach((d) => {
        const dance = this.statsIdMap?.get(d) as TypeStats;
        const groups = dance.groups ?? [];
        groups.push(g);
        dance.groups = groups;
      })
    );
  }

  public fromId(id: string): DanceStats | undefined {
    return this.flatStats.find((d) => id === d.id);
  }

  public fromName(name: string): DanceStats | undefined {
    const n = name.toLowerCase();
    return this.flatStats.find((d) => n === d.name.toLowerCase());
  }

  public get flatStats(): DanceStats[] {
    if (!this.tree || !this.dances) {
      throw new Error(
        "Attempted to call flatStats on an uninitialized DanceEnvironment"
      );
    }
    return [...this.tree, ...this.dances];
  }

  public get flatDances(): NamedObject[] {
    if (!this.tree || !this.dances) {
      throw new Error(
        "Attempted to call flatDances on an uninitialized DanceEnvironment"
      );
    }
    return [...this.tree, ...this.dances];
  }

  public get flatTypes(): TypeStats[] {
    return this.dances!;
  }

  public get flatInstances(): DanceInstance[] {
    return this.flatTypes.flatMap((d) => d && d.instances);
  }

  public get groupedStats(): DanceStats[] {
    return this.tree!.sort((a, b) => a.name.localeCompare(b.name)).flatMap(
      (group) => [
        group,
        ...group.dances.sort((a, b) => a.name.localeCompare(b.name)),
      ]
    );
  }

  public get styles(): string[] {
    const styles = this.flatInstances.map((inst) => inst && inst.style);
    return [...new Set(styles)].sort();
  }

  public get types(): string[] {
    return this.groups!.map((s) => s.name);
  }

  public static filterByName(
    dances: DanceStats[],
    nameFilter: string,
    includeChildren = false
  ): DanceStats[] {
    const filter = nameFilter.toLowerCase();
    return dances.filter(
      (d) =>
        d.songCount > 0 &&
        (!filter ||
          d.name.toLowerCase().indexOf(filter) !== -1 ||
          (includeChildren &&
            d.isGroup &&
            (d as GroupStats).dances.find(
              (c) =>
                c.songCount > 0 && c.name.toLowerCase().indexOf(filter) !== -1
            )))
    );
  }
}
