import { jsonMember, jsonObject } from "typedjson";
import { Meter } from "./Meter";
import { NamedObject } from "./NamedObject";
import { TempoRange } from "./TempoRange";

@jsonObject
export class DanceObject extends NamedObject {
  @jsonMember public meter!: Meter;
  @jsonMember public tempoRange!: TempoRange;
  @jsonMember public blogTag?: string;

  public get baseId(): string {
    return this.id.substr(0, 3);
  }
}
