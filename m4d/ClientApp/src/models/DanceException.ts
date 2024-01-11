import { jsonMember, jsonObject } from "typedjson";
import { DanceObject } from "./DanceObject";
import { assign } from "@/helpers/ObjectHelpers";

// INT-TODO:: Should this realy derive from DanceObject?
//  It doesn't support most of the properties (including id & name)
//  Maybe we just need to add TempoRange to this and not derive from DanceObject?
@jsonObject
export class DanceException extends DanceObject {
  @jsonMember(String) public organization!: string;

  public constructor(init?: Partial<DanceException>) {
    super();
    assign(this, init);
  }

  public matchesFilter(filter: string): boolean {
    return this.organization.toLowerCase() === filter.toLowerCase();
  }
}
