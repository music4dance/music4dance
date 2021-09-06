import { jsonMember, jsonObject } from "typedjson";
import { kebabToWords } from "@/helpers/StringHelpers";
import { Tag } from "./Tag";
import { TagList } from "./TagList";
import { TempoRange } from "./TempoRange";
import { DanceStats } from "./DanceStats";
import { DanceFilter } from "./DanceFilter";
import { DanceType } from "./DanceType";
import { GroupStats } from "./GroupStats";

@jsonObject
export class TypeStats extends DanceType implements DanceStats {
  @jsonMember public songCount!: number;
  @jsonMember public maxWeight!: number;
  @jsonMember public songTags!: string;

  public groups?: GroupStats[];

  public get isGroup(): boolean {
    return false;
  }

  public get tags(): Tag[] {
    return new TagList(this.songTags).tags;
  }

  public filterByTempo(
    beatsPerMinute: number,
    beatsPerMeasure: number,
    percentEpsilon: number
  ): boolean {
    if (!this.isCompatibleMeter(beatsPerMeasure)) {
      return false;
    }

    return this.isCompatibleTempo(
      beatsPerMinute,
      beatsPerMeasure,
      percentEpsilon
    );
  }

  public inGroup(groupId: string): boolean {
    const groups = this.groups;
    return !!groups && !!groups.find((g) => g.id === groupId);
  }

  public inGroupName(groupName: string): boolean {
    const groups = this.groups;
    return !!groups && !!groups.find((g) => g.name === groupName);
  }

  public match(filter: DanceFilter): boolean {
    return (
      filter.types.find((t) => this.inGroupName(kebabToWords(t))) != null &&
      filter.meters.find(
        (m) => m === `${this.meter.numerator}-${this.meter.denominator}`
      ) != null &&
      filter.styles.find((s) =>
        this.instances.find((inst) => kebabToWords(s) === inst.style)
      ) != null
    );
  }

  public isCompatibleTempo(
    beatsPerMinute: number,
    beatsPerMeasure: number,
    percentEpsilon: number
  ): boolean {
    const range = this.getFuzzyTempoRange(percentEpsilon);

    return beatsPerMinute >= range.min && beatsPerMinute <= range.max;
  }

  public isCompatibleMeter(beatsPerMeasure: number): boolean {
    if (!beatsPerMeasure || beatsPerMeasure === 1) {
      return true;
    }

    const numerator = this.meter.numerator;
    if (beatsPerMeasure === numerator) {
      return true;
    }

    //  2/4 and 4/4 are basically compatible meters as far as most dancing is concerned
    return (
      (beatsPerMeasure === 2 && numerator === 4) ||
      (beatsPerMeasure === 4 && numerator === 2)
    );
  }

  public getFuzzyTempoRange(percentEpsilon: number): TempoRange {
    const range = this.tempoRange.toBpm(this.meter.numerator);
    const average = (range.min + range.max) / 2;
    const epsBpm = percentEpsilon * (average / 100);

    return new TempoRange(range.min - epsBpm, range.max + epsBpm);
  }
}
