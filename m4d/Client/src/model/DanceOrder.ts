import "reflect-metadata";
import { DanceStats, TempoRange } from "./DanceStats";

export class DanceOrder {
  public dance: DanceStats;
  public bpmDelta: number;

  constructor(stats: DanceStats, target: number) {
    this.dance = stats;
    this.bpmDelta = this.computeDelta(target);
  }

  public get name(): string {
    return this.dance.danceName;
  }

  public get mpmDelta(): number {
    return this.bpmDelta / this.numerator;
  }

  public get rangeMpm(): TempoRange {
    return this.dance.danceType!.tempoRange;
  }

  public get rangeBpm(): TempoRange {
    return this.dance.tempoRange;
  }

  public get rangeMpmFormatted(): string {
    return this.rangeMpm.toString() + " MPM (" + this.numerator + "/4)";
  }

  public get rangeBpmFormatted() {
    return this.rangeBpm.toString() + " BPM";
  }

  private get numerator() {
    return this.dance.danceType!.meter.numerator;
  }

  private computeDelta(target: number): number {
    return this.dance.tempoRange.computeDelta(target);
  }

  public static dancesForTempo(
    stats: DanceStats[],
    beatsPerMinute: number,
    beatsPerMeasure: number,
    percentEpsilon = 5
  ): DanceOrder[] {
    // return danceStats.flatMap((group: DanceStats) => group.children);  TODO: See if we can find a general polyfill

    return stats
      .map((group: DanceStats) => group.children)
      .reduce((acc: DanceStats[], val: DanceStats[]) => acc.concat(val))
      .filter(
        (d: DanceStats) =>
          d && d.filterByTempo(beatsPerMinute, beatsPerMeasure, percentEpsilon),
        []
      )
      .map((d: DanceStats) => new DanceOrder(d, beatsPerMinute))
      .sort(
        (a: DanceOrder, b: DanceOrder) =>
          Math.abs(a.bpmDelta) - Math.abs(b.bpmDelta)
      );
  }
}
