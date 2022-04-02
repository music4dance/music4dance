import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";
import { Meter } from "./Meter";
import { NamedObject } from "./NamedObject";
import { TempoRange } from "./TempoRange";

// TODONEXT:
// Make sure "Other" is behaving...
// Write the code to take a string and do a soft match against
//   all of name, synonyms and searchonyms (do we want to include id?)
//   We should definitely expand 2 into two.  Remove all non-alpha
//   including spaces on both sides
@jsonObject
export class DanceObject extends NamedObject {
  @jsonMember public meter!: Meter;
  @jsonMember public tempoRange!: TempoRange;
  @jsonMember public blogTag?: string;

  public get baseId(): string {
    return this.id.substr(0, 3);
  }
}
