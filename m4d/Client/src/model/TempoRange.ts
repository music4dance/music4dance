import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class TempoRange {
  @jsonMember public min!: number;
  @jsonMember public max!: number;

  constructor(min = 0, max = 0) {
    this.min = min;
    this.max = max;
  }

  public toString(separator = "-"): string {
    return (
      this.formatTempo(this.min) +
      (this.min === this.max ? "" : separator + this.formatTempo(this.max))
    );
  }

  public bpm(numerator: number, separator = "-"): string {
    return (
      this.formatTempo(this.min * numerator) +
      (this.min === this.max
        ? ""
        : separator + this.formatTempo(this.max * numerator))
    );
  }

  public mpm(numerator: number, separator = "-"): string {
    return (
      this.formatTempo(this.min / numerator) +
      (this.min === this.max
        ? ""
        : separator + this.formatTempo(this.max / numerator))
    );
  }

  public computeDelta(target: number): number {
    if (target < this.min) {
      return target - this.min;
    }

    if (target > this.max) {
      return target - this.max;
    }

    return 0;
  }

  public combine(that: TempoRange): TempoRange {
    return new TempoRange(
      Math.min(this.min, that.min),
      Math.max(this.max, that.max)
    );
  }

  public toBpm(numerator: number): TempoRange {
    return new TempoRange(this.min * numerator, this.max * numerator);
  }

  private formatTempo(tempo: number): string {
    return parseFloat(tempo.toFixed(1)).toString();
  }
}
