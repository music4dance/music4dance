import { jsonMember, jsonObject } from "typedjson";
import { TempoRange } from "./TempoRange";
import { Meter } from "./Meter";
import { NamedObject } from "./NamedObject";

@jsonObject
export class DanceObject extends NamedObject {
  @jsonMember public meter!: Meter;
  @jsonMember public tempoRange!: TempoRange;
  @jsonMember public blogTag?: string;

  public get baseId(): string {
    return this.id.substr(0, 3);
  }
}
