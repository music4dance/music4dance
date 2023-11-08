import { jsonMember, jsonObject } from "typedjson";
import { Meter } from "./Meter";
import { NamedObject } from "./NamedObject";
import { TempoRange } from "./TempoRange";

@jsonObject
export class DanceObject extends NamedObject {
  @jsonMember(Meter) public meter!: Meter;
  @jsonMember(TempoRange) public tempoRange!: TempoRange;
  @jsonMember(String) public blogTag?: string;

  public get baseId(): string {
    return this.id.substr(0, 3);
  }
}
