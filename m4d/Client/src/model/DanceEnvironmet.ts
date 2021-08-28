import "reflect-metadata";
import { jsonObject, TypedJSON, jsonArrayMember } from "typedjson";
import {
  DanceGroup,
  DanceInstance,
  TypeStats,
  DanceType,
  DanceStats,
  GroupStats,
} from "./DanceStats";
import { TagDatabase } from "./TagDatabase";
import { TagGroup } from "./TagGroup";

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
  @jsonArrayMember(TagGroup) public tagGroups?: TagGroup[];
  @jsonArrayMember(TagGroup) public incrementalTags?: TagGroup[];

  public tree?: GroupStats[];

  public rehydrate(): void {
    this.tree = this.groups?.map((g) => new GroupStats(g, this.dances!));
  }

  public get tagDatabase(): TagDatabase {
    if (!this._tagDatabase && this.tagGroups) {
      this._tagDatabase = new TagDatabase(this.tagGroups, this.incrementalTags);
    }

    return this._tagDatabase ?? new TagDatabase();
  }
  private _tagDatabase?: TagDatabase;

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

  public get flatTypes(): DanceType[] {
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
}
