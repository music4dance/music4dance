import "reflect-metadata";
import { jsonObject, TypedJSON, jsonArrayMember } from "typedjson";
import { DanceInstance, DanceStats, DanceType } from "./DanceStats";
import { TagDatabase } from "./TagDatabase";
import { TagGroup } from "./TagGroup";

TypedJSON.setGlobalConfig({
  errorHandler: (e) => {
    console.error(e);
    throw e;
  },
});

@jsonObject
export class DanceEnvironment {
  @jsonArrayMember(DanceStats, { name: "tree" }) public stats?: DanceStats[];
  @jsonArrayMember(TagGroup) public tagGroups?: TagGroup[];
  @jsonArrayMember(TagGroup) public incrementalTags?: TagGroup[];

  public get tagDatabase(): TagDatabase {
    if (!this._tagDatabase && this.tagGroups) {
      this._tagDatabase = new TagDatabase(this.tagGroups, this.incrementalTags);
    }

    return this._tagDatabase ?? new TagDatabase();
  }
  private _tagDatabase?: TagDatabase;

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
