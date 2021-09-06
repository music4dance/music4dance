import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class Meter {
  @jsonMember public numerator!: number;
  @jsonMember public denominator!: number;

  public toString(): string {
    return `${this.numerator}/${this.denominator}`;
  }
}
