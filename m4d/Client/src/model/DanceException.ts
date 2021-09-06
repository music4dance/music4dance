import { jsonMember, jsonObject } from "typedjson";
import { DanceObject } from "./DanceObject";

@jsonObject
export class DanceException extends DanceObject {
  @jsonMember public organization!: string;
  @jsonMember public competitor!: string;
  @jsonMember public level!: string;

  public matchesFilter(filter: string): boolean {
    const parts = filter.split("-");
    if (this.organization.toLowerCase() !== parts[0].toLowerCase()) {
      return false;
    }
    if (parts.length === 1) {
      return true;
    }
    if (
      parts[1] === "1" &&
      (this.level === "Bronze" || this.competitor === "ProAm")
    ) {
      return true;
    }
    if (
      parts[1] === "2" &&
      (this.level === "Silver,Gold" ||
        this.competitor === "Professional,Amateur")
    ) {
      return true;
    }
    return false;
  }
}
