import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import type { NamedObject } from "@/models/DanceDatabase/NamedObject";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import type { DanceThreshold } from "./DanceThreshold";

export class DanceQueryBase {
  public get danceThresholds(): DanceThreshold[] {
    throw new Error("Not Implemented");
  }

  public get danceList(): string[] {
    return this.danceThresholds.map((d) => d.id);
  }

  public get isExclusive(): boolean {
    return false;
  }

  public get dances(): NamedObject[] {
    return this.danceList.map((id) => safeDanceDatabase().fromId(id)!);
  }

  public get danceNames(): string[] {
    return this.dances.map((d) => d.name);
  }

  public get singleDance(): boolean {
    return this.danceList.length === 1 && !DanceGroup.isGroup(this.dances[0]);
  }

  public get isSimple(): boolean {
    const c = this.danceThresholds.length;
    return c === 0 || (c === 1 && this.danceThresholds[0].threshold === 1);
  }

  public get description(): string {
    return "Unknown Dances";
  }

  public get shortDescription(): string {
    return "Unknown Dances";
  }

  public get query(): string {
    return "";
  }
}
