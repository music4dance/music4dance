import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class TempoRange {
  @jsonMember(Number) public min!: number;
  @jsonMember(Number) public max!: number;

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

  public bpm(numerator: number = 1, separator = "-"): string {
    return (
      this.formatTempo(this.min * numerator) +
      (this.min === this.max ? "" : separator + this.formatTempo(this.max * numerator))
    );
  }

  public formatBPM(numerator = 1, separator = "-"): string {
    return `${this.bpm(numerator, separator)} BPM`;
  }

  public mpm(numerator: number = 1, separator = "-"): string {
    return (
      this.formatTempo(this.min / numerator) +
      (this.min === this.max ? "" : separator + this.formatTempo(this.max / numerator))
    );
  }

  public formatMPM(numerator = 1, separator = "-"): string {
    return `${this.bpm(numerator, separator)} MPM`;
  }

  public calculateDelta(tempo: number): number {
    return tempo > this.max ? tempo - this.max : tempo < this.min ? tempo - this.min : 0;
  }

  public calculateDeltaPercent(tempo: number) {
    return (this.calculateDelta(tempo) * 100) / (tempo >= this.max ? this.max : this.min);
  }

  public include(that?: TempoRange | null): TempoRange {
    return that ? new TempoRange(Math.min(this.min, that.min), Math.max(this.max, that.max)) : this;
  }

  public get isInfinite(): boolean {
    return this.min < 10 && this.max > 499;
  }

  public toBpm(numerator: number): TempoRange {
    return new TempoRange(this.min * numerator, this.max * numerator);
  }

  public toMpm(numerator: number): TempoRange {
    return new TempoRange(this.min / numerator, this.max / numerator);
  }

  private formatTempo(tempo: number): string {
    return parseFloat(tempo.toFixed(1)).toString();
  }
}
