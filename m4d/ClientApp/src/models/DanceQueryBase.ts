import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import type { NamedObject } from "@/models/DanceDatabase/NamedObject";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import type { DanceQueryItem } from "./DanceQueryItem";

export class DanceQueryBase {
  public get danceQueryItems(): DanceQueryItem[] {
    throw new Error("Not Implemented");
  }

  public get danceList(): string[] {
    return this.danceQueryItems.map((d) => d.id);
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
    const dance = this.dances[0];
    return this.danceList.length === 1 && dance ? !DanceGroup.isGroup(dance) : false;
  }

  public get isSimple(): boolean {
    const c = this.danceQueryItems.length;
    return (
      c === 0 ||
      (c === 1 && this.danceQueryItems[0]?.threshold === 1 && !this.danceQueryItems[0]?.tags)
    );
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
