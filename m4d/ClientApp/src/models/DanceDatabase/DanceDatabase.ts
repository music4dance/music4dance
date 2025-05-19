import { DanceType } from "./DanceType";
import { DanceGroup } from "./DanceGroup";
import { TypedJSON, jsonArrayMember, jsonObject } from "typedjson";
import type { NamedObject } from "./NamedObject";
import type { DanceInstance } from "./DanceInstance";
import type { DanceFilter } from "./DanceFilter";
import { DanceOrder } from "./DanceOrder";
import { DanceMetrics } from "./DanceMetrics";

TypedJSON.setGlobalConfig({
  errorHandler: (e) => {
    console.error(e);
    throw e;
  },
});

@jsonObject({ onDeserialized: "onDeserialized" })
export class DanceDatabase {
  @jsonArrayMember(DanceType) dances!: DanceType[];
  @jsonArrayMember(DanceGroup) groups!: DanceGroup[];
  @jsonArrayMember(DanceMetrics) metrics!: DanceMetrics[];

  public constructor(init?: Partial<DanceDatabase>) {
    Object.assign(this, init);
  }

  public static load(json: string): DanceDatabase {
    const ret = TypedJSON.parse(json, DanceDatabase);
    if (!ret) {
      throw new Error("Failed to parse DanceDatabase");
    }
    return ret;
  }

  public get all(): NamedObject[] {
    return [...this.dances, ...this.groups];
  }

  public get flattened(): NamedObject[] {
    return this.groups.reduce((acc, x) => [...acc, x, ...x.dances], [] as NamedObject[]);
  }

  public get flatGroups(): NamedObject[] {
    return this.groups
      .sort((a, b) => a.name.localeCompare(b.name))
      .flatMap((group) => [group, ...group.dances.sort((a, b) => a.name.localeCompare(b.name))]);
  }

  public danceFromId(id: string): DanceType | undefined {
    return this.dances.find((d) => d.id === id);
  }

  public instanceFromId(id: string): DanceInstance | undefined {
    const dance = this.danceFromId(id.substring(0, 3));
    if (!dance) {
      return undefined;
    }
    return dance?.instances.find((i) => i.id === id);
  }

  public filterByName(nameFilter: string, includeChildren = false): NamedObject[] {
    return DanceDatabase.filterByName(this.all, nameFilter, includeChildren);
  }

  public fromId(id: string): NamedObject | undefined {
    return this.all.find((x) => x.id === id);
  }

  public fromName(name: string): NamedObject | undefined {
    return this.all.find((x) => x.name === name);
  }

  public fromSynonym(name: string): NamedObject | undefined {
    const d = this.fromName(name);
    if (d) return d;

    const n = this.flattened.find((x) => x.isMatch(name));
    return n ? this.fromId(n.id) : undefined;
  }

  public metricsFromId(id: string): DanceMetrics {
    const metrics = this.metrics.find((m) => m.id === id);
    if (!metrics) {
      console.log("No metrics found for id", id);
      return new DanceMetrics({ id, songCount: 0, maxWeight: 0 });
    }
    return metrics;
  }

  public getSongCount(id: string): number {
    return this.metricsFromId(id).songCount;
  }

  public getMaxWeight(id: string): number {
    return this.metricsFromId(id).maxWeight;
  }

  public filter(filter: DanceFilter): DanceDatabase {
    const dances = filter.filter(this.dances);
    const groups = [
      ...this.dances.reduce((acc, x) => {
        x.groups.forEach((g) => acc.add(g.name));
        return acc;
      }, new Set<string>()),
    ].map((g) => this.groups.find((x) => x.name === g)!);
    return new DanceDatabase({ dances, groups });
  }

  public filterDances(filter: DanceFilter, tempo: number, epsilon: number): DanceOrder[] {
    return DanceDatabase.filterTempo(filter.filter(this.dances), tempo, epsilon);
  }

  public static filterTempo(dances: DanceType[], tempo: number, epsilon: number): DanceOrder[] {
    return dances
      .map((dance) => DanceOrder.create(dance, tempo))
      .filter((order) => order.deltaPercentAbsolute < epsilon)
      .sort((a, b) => a.deltaPercentAbsolute - b.deltaPercentAbsolute);
  }

  // TODO: Can we generalize this?
  public get styles(): string[] {
    return [
      ...this.dances.reduce((acc, x) => {
        x.styles?.forEach((s) => acc.add(s));
        return acc;
      }, new Set<string>()),
    ].sort();
  }

  public get organizations(): string[] {
    return [
      ...this.dances.reduce((acc, x) => {
        x.organizations?.forEach((s) => acc.add(s));
        return acc;
      }, new Set<string>()),
    ].sort();
  }

  public static filterByName(
    dances: NamedObject[],
    nameFilter: string,
    includeChildren = false,
  ): NamedObject[] {
    const filter = nameFilter.toLowerCase();
    return dances.filter(
      (d) =>
        !filter ||
        d.hasString(filter) ||
        (includeChildren &&
          DanceGroup.isGroup(d) &&
          (d as DanceGroup).dances.find((c) => c.hasString(filter))),
    );
  }

  private onDeserialized(): void {
    for (const group of this.groups) {
      group.dances = group.danceIds.map((id) => this.dances.find((d) => d.id === id)!);

      for (const dance of group.dances) {
        dance.groups.push(group);
      }
    }
  }
}
