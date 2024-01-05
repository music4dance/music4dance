import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";
import { DanceObject } from "./DanceObject";

@jsonObject
export class DanceException extends DanceObject {
  @jsonMember public organization!: string;
  @jsonMember public competitor!: string;
  @jsonMember public level!: string;

  public matchesFilter(filter: string): boolean {
    const parts = filter.split("-");
    // INT-TODO: We're ignoring the second part for now, but we should just get rid of it completely
    return this.organization.toLowerCase() === parts[0].toLowerCase();
  }
}
