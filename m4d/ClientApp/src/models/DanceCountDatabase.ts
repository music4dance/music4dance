import type { NamedObject } from "./NamedObject";
import { DanceDatabase } from "./DanceDatabase";
import { DanceType } from "./DanceType";
import { DanceGroup } from "./DanceGroup";
import { DanceTypeCount } from "./DanceTypeCount";

export class DanceCountDatabase extends DanceDatabase {
  public counts: Record<string, number>;

  public constructor(danceDatabase: DanceDatabase, counts: Record<string, number>) {
    super(danceDatabase);
    this.counts = counts;
  }

  public get all(): NamedObject[] {
    return this.mapToCount(super.all);
  }

  public filterByName(name: string): NamedObject[] {
    return this.mapToCount(super.filterByName(name));
  }

  private mapToCount(objs: NamedObject[]): NamedObject[] {
    return objs
      .map((x) =>
        DanceGroup.isGroup(x) ? x : new DanceTypeCount(x as DanceType, this.counts[x.id]),
      )
      .filter((x) => DanceGroup.isGroup(x) || (x as DanceTypeCount).count > 0);
  }
}
