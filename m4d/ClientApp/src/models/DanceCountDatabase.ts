import type { NamedObject } from "@/models/DanceDatabase/NamedObject";
import { DanceDatabase } from "@/models/DanceDatabase/DanceDatabase";
import { DanceType } from "@/models/DanceDatabase/DanceType";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
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
