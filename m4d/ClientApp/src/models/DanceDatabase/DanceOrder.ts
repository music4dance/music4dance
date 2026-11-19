import { DanceType } from "./DanceType";

export class DanceOrder {
  public dance: DanceType;
  public delta: number;
  public deltaPercent: number;
  public pitchPercent: number;
  public get deltaPercentAbsolute(): number {
    return Math.abs(this.deltaPercent);
  }
  public get deltaMpm(): number {
    return this.delta / this.dance.meter.numerator;
  }

  public static create(dance: DanceType, tempo: number): DanceOrder {
    return new DanceOrder(
      dance,
      dance.tempoRange.calculateDelta(tempo),
      dance.tempoRange.calculateDeltaPercent(tempo),
      dance.tempoRange.calculatePitchPercent(tempo),
    );
  }

  private constructor(
    dance: DanceType,
    tempoDelta: number,
    tempoDeltaPercent: number,
    pitchPercent: number,
  ) {
    this.dance = dance;
    this.delta = tempoDelta;
    this.deltaPercent = tempoDeltaPercent;
    this.pitchPercent = pitchPercent;
  }

  public toString(): string {
    const style =
      this.dance.instances.length > 0
        ? this.dance.instances.map((inst) => inst.style).join(", ")
        : "";
    return `${this.dance.name}: Style=(${style}), Delta=(${this.tempoDeltaString})`;
  }

  private get tempoDeltaString(): string {
    const delta = this.deltaMpm;
    return Math.abs(delta) < 0.01
      ? ""
      : delta < 0
        ? `${delta.toFixed(2)}MPM`
        : `+${delta.toFixed(2)}MPM`;
  }
}
