import "reflect-metadata";
import { TypeStats } from "./TypeStats";
import { TempoRange } from "./TempoRange";

export class DanceOrder {
  public dance: TypeStats;
  public bpmDelta: number;

  constructor(stats: TypeStats, target: number) {
    this.dance = stats;
    this.bpmDelta = this.computeDelta(target);
  }

  public get name(): string {
    return this.dance.name;
  }

  public get mpmDelta(): number {
    return this.bpmDelta / this.numerator;
  }

  public get rangeMpm(): TempoRange {
    return this.dance!.tempoRange;
  }

  public get rangeBpm(): TempoRange {
    return this.dance.tempoRange;
  }

  public get rangeMpmFormatted(): string {
    return this.rangeMpm.toString() + " MPM (" + this.numerator + "/4)";
  }

  public get rangeBpmFormatted(): string {
    return this.rangeBpm.toString() + " BPM";
  }

  private get numerator() {
    return this.dance!.meter.numerator;
  }

  private computeDelta(target: number): number {
    return this.dance.tempoRange
      .toBpm(this.dance.meter.numerator)
      .computeDelta(target);
  }

  public static dancesForTempo(
    stats: TypeStats[],
    beatsPerMinute: number,
    beatsPerMeasure: number,
    percentEpsilon = 5
  ): DanceOrder[] {
    // return danceStats.flatMap((group: DanceStats) => group.children);  TODO: See if we can find a general polyfill

    return stats
      .filter(
        (d: TypeStats) =>
          d.filterByTempo(beatsPerMinute, beatsPerMeasure, percentEpsilon),
        []
      )
      .map((d: TypeStats) => new DanceOrder(d, beatsPerMinute))
      .sort(
        (a: DanceOrder, b: DanceOrder) =>
          Math.abs(a.bpmDelta) - Math.abs(b.bpmDelta)
      );
  }
}
