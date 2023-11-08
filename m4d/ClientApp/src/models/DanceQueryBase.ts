import { DanceEnvironment } from "./DanceEnvironment";
import type { DanceStats } from "./DanceStats";

declare const environment: DanceEnvironment;

export class DanceQueryBase {
  public get danceList(): string[] {
    throw new Error("Not Implemented");
  }

  public get isExclusive(): boolean {
    return false;
  }

  public get dances(): DanceStats[] {
    return this.danceList.map((id) => environment!.fromId(id)!);
  }

  public get danceNames(): string[] {
    return this.dances.map((d) => d.name);
  }

  public get singleDance(): boolean {
    return this.danceList.length === 1 && !this.dances[0].isGroup;
  }

  public get isSimple(): boolean {
    return this.danceList.length < 2;
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
