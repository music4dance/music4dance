import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class Meter {
  @jsonMember(Number) public numerator!: number;
  @jsonMember(Number) public denominator!: number;

  constructor(numerator = 0, denominator = 0) {
    this.numerator = numerator;
    this.denominator = denominator;
  }

  public equals(that: Meter): boolean {
    return this.numerator === that.numerator && this.denominator === that.denominator;
  }

  public toString(): string {
    return `${this.numerator}/${this.denominator}`;
  }

  public static get EmptyMeter(): Meter {
    const meter = new Meter();
    meter.numerator = 0;
    meter.denominator = 0;
    return meter;
  }
}
