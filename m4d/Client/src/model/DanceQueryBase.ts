import { DanceEnvironment } from "./DanceEnvironmet";
import { DanceStats } from "./DanceStats";

declare const environment: DanceEnvironment;

export class DanceQueryBase {
  public get danceList(): string[] {
    throw new Error("Not Implemented");
  }

  public get isExclusive(): boolean {
    return false;
  }

  public get includeInferred(): boolean {
    return false;
  }

  public get dances(): DanceStats[] {
    return this.danceList.map((id) => environment!.fromId(id)!);
  }

  public get danceNames(): string[] {
    return this.dances.map((d) => d.danceName);
  }

  public get singleDance(): boolean {
    return this.danceList.length === 1;
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
