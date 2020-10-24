/* tslint:disable:max-classes-per-file */
import "reflect-metadata";
import { jsonMember, jsonObject, jsonArrayMember } from "typedjson";
import { kebabToWords, wordsToKebab } from "@/helpers/StringHelpers";

@jsonObject
export class TempoRange {
  @jsonMember public min!: number;
  @jsonMember public max!: number;

  constructor(min: number = 0, max: number = 0) {
    this.min = min;
    this.max = max;
  }

  public toString(): string {
    return (
      this.formatTempo(this.min) +
      (this.min === this.max ? "" : "-" + this.formatTempo(this.max))
    );
  }

  public bpm(numerator: number): string {
    return (
      this.formatTempo(this.min * numerator) +
      (this.min === this.max
        ? ""
        : "-" + this.formatTempo(this.max * numerator))
    );
  }

  public mpm(numerator: number): string {
    return (
      this.formatTempo(this.min / numerator) +
      (this.min === this.max
        ? ""
        : "-" + this.formatTempo(this.max / numerator))
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

@jsonObject
export class Meter {
  @jsonMember public numerator!: number;
  @jsonMember public denominator!: number;

  public toString(): string {
    return `${this.numerator}/${this.denominator}`;
  }
}

@jsonObject
export class DanceObject {
  @jsonMember public id!: string;
  @jsonMember public name!: string;
  @jsonMember public meter!: Meter;
  @jsonMember public tempoRange!: TempoRange;

  public get baseId(): string {
    return this.id.substr(0, 3);
  }
}

@jsonObject
export class DanceGroup extends DanceObject {
  @jsonArrayMember(String) public danceIds!: string[];
}

@jsonObject
export class DanceException extends DanceObject {
  @jsonMember public organization!: string;
  @jsonMember public competitor!: string;
  @jsonMember public level!: string;

  public matchesFilter(filter: string): boolean {
    const parts = filter.split("-");
    if (this.organization.toLowerCase() !== parts[0].toLowerCase()) {
      return false;
    }
    if (parts.length === 1) {
      return true;
    }
    if (
      parts[1] === "1" &&
      (this.level === "Bronze" || this.competitor === "ProAm")
    ) {
      return true;
    }
    if (
      parts[1] === "2" &&
      (this.level === "Silver,Gold" ||
        this.competitor === "Professional,Amateur")
    ) {
      return true;
    }
    return false;
  }
}

@jsonObject
export class DanceInstance extends DanceObject {
  @jsonMember public style!: string;
  @jsonMember public competitionGroup!: string;
  @jsonMember public compititionOrder!: number;
  @jsonArrayMember(DanceException) public exceptions!: DanceException[];

  public filteredTempo(organizations: string[]): TempoRange | undefined {
    if (!organizations.length) {
      return this.tempoRange;
    }

    // First - if any choice doesn't have an explicit exception,
    //  just return the instance tempo range
    const excs = this.exceptionsFromOrganization(organizations);
    if (excs.length !== organizations.length) {
      return this.tempoRange;
    }

    let ret: TempoRange | undefined;
    for (const exc of excs) {
      if (!exc.tempoRange) {
        continue;
      }
      const newRange = exc.tempoRange;
      // tslint:disable-next-line: prefer-conditional-expression
      if (!ret) {
        ret = newRange;
      } else {
        ret = ret.combine(newRange);
      }
    }
    return ret;
  }

  public get styleFamily(): string {
    return this.style.split(" ")[0];
  }

  private exceptionsFromOrganization(
    organizations: string[]
  ): DanceException[] {
    return this.exceptions.filter((e) =>
      organizations.find((o) => e.matchesFilter(o))
    );
  }

  public get shortName(): string {
    const styleFamily = this.styleFamily;
    return this.name.startsWith(styleFamily + " ")
      ? this.name.substring(styleFamily.length + 1)
      : this.name;
  }
}

export interface DanceFilter {
  styles: string[];
  meters: string[];
  types: string[];
}

@jsonObject
export class DanceType extends DanceObject {
  @jsonArrayMember(String) public organizations!: string[];
  @jsonMember public groupName!: string;
  @jsonMember public groupId!: string;
  @jsonMember public link!: string;
  @jsonArrayMember(DanceInstance) public instances!: DanceInstance[];

  public get styles(): string[] {
    return this.instances.map((inst) => inst.style);
  }

  public get seoName(): string {
    return wordsToKebab(this.name);
  }

  public filteredStyles(filter: string[]): string[] {
    return this.styles.filter((s) => filter.indexOf(wordsToKebab(s)) !== -1);
  }

  public match(filter: DanceFilter): boolean {
    return (
      filter.types.find((t) => kebabToWords(t) === this.groupName) != null &&
      filter.meters.find(
        (m) => m === `${this.meter.numerator}-${this.meter.denominator}`
      ) != null &&
      filter.styles.find((s) =>
        this.instances.find((inst) => kebabToWords(s) === inst.style)
      ) != null
    );
  }

  public filteredTempo(
    styles: string[],
    organizations: string[]
  ): TempoRange | undefined {
    if (!styles.length && !organizations.length) {
      return this.tempoRange;
    }

    let ret: TempoRange | undefined;
    for (const inst of this.instances) {
      if (
        !inst.tempoRange ||
        (styles.length && !styles.find((s) => s === wordsToKebab(inst.style)))
      ) {
        continue;
      }

      const newRange = inst.filteredTempo(organizations);
      if (!ret) {
        ret = newRange;
      } else if (newRange) {
        ret = ret.combine(newRange);
      }
    }

    return ret;
  }

  private instanceFromStyle(style: string): DanceInstance | undefined {
    return this.instances.find((i) => i.style === kebabToWords(style));
  }

  // TODO: should we throw out the whole thing if the dancetype isn't specified by the organization?
  private buildOrganizationIds(organizations: string[]): string[] {
    const prefixes = ["Social", "DanceSport", "NDCA"];
    return prefixes.filter((p) =>
      organizations.find((o) => o.indexOf(p) !== -1)
    );
  }
}

@jsonObject
export class DanceStats {
  @jsonMember public danceId!: string;
  @jsonMember public danceName!: string;
  @jsonMember public blogTag!: string;
  @jsonMember public seoName!: string;
  @jsonMember public description!: string;
  @jsonMember public songCount!: number;
  @jsonMember public songCountExplicit!: number;
  @jsonMember public songCountImplicit!: number;
  @jsonMember public maxWeight!: number;
  @jsonMember({ constructor: DanceType }) public danceType!: DanceType | null;
  @jsonMember({ constructor: DanceGroup })
  public danceGroup!: DanceGroup | null;
  @jsonArrayMember(DanceStats) public children!: DanceStats[];

  public get tempoRange(): TempoRange {
    const numerator = this.danceType!.meter.numerator;
    const range = this.danceType!.tempoRange;

    return new TempoRange(numerator * range.min, numerator * range.max);
  }

  public get styles(): string[] {
    return this!.danceType!.styles;
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

    const numerator = this.danceType!.meter.numerator;
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
    const range = this.tempoRange;
    const average = (range.min + range.max) / 2;
    const epsBpm = percentEpsilon * (average / 100);

    return new TempoRange(range.min - epsBpm, range.max + epsBpm);
  }
}
