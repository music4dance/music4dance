import { jsonMember, jsonObject } from "typedjson";
import { Meter } from "./Meter";
import { NamedObject } from "./NamedObject";
import { TempoRange } from "./TempoRange";

@jsonObject
export class DanceObject extends NamedObject {
  @jsonMember(Meter, { name: "meter" }) public internalMeter!: Meter;
  @jsonMember(TempoRange, { name: "tempoRange" })
  public internalTempoRange!: TempoRange;
  @jsonMember(String) public blogTag?: string;

  public get meter(): Meter {
    return this.internalMeter;
  }

  public get tempoRange(): TempoRange {
    return this.internalTempoRange;
  }

  public constructor(init?: Partial<DanceObject>) {
    super();
    Object.assign(this, init);
  }

  public get baseId(): string {
    return this.id.substring(0, 3);
  }
}
