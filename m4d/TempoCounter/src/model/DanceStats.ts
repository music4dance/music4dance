/* tslint:disable:max-classes-per-file */
import 'reflect-metadata';
import { jsonMember, jsonObject, jsonArrayMember} from 'typedjson';

@jsonObject export class TempoRange {
    @jsonMember public min!: number;
    @jsonMember public max!: number;

    constructor(min: number = 0, max: number = 0) {
        this.min = min;
        this.max = max;
    }

    public get format(): string {
        return this.formatTempo(this.min) +
            ((this.min === this.max) ? '' : '-' + this.formatTempo(this.max));    }

    public computeDelta(target: number): number {
        if (target < this.min) {
            return target - this.min;
        }

        if (target > this.max) {
            return target - this.max;
        }

        return 0;
    }

    private formatTempo(tempo: number): string {
        return parseFloat(tempo.toFixed(1)).toString();
    }
}

@jsonObject export class Meter {
    @jsonMember public numerator!: number;
    @jsonMember public denominator!: number;
}

@jsonObject export class DanceObject {
    @jsonMember public id!: string;
    @jsonMember public name!: string;
    @jsonMember public meter!: Meter;
    @jsonMember public tempoRange!: TempoRange;
}

@jsonObject export class DanceGroup extends DanceObject {
    @jsonArrayMember(String) public danceIds!: string[];
}

@jsonObject export class DanceException extends DanceObject {
    @jsonMember public organization!: string;
    @jsonMember public competitor!: string;
    @jsonMember public level!: string;
}

@jsonObject export class DanceInstance extends DanceObject {
    @jsonMember public style!: string;
    @jsonMember public competitionGroup!: string;
    @jsonMember public compititionOrder!: number;
    @jsonArrayMember(DanceException) public exceptions!: DanceException[];
}

@jsonObject export class DanceType extends DanceObject {
    @jsonArrayMember(String) public organizations!: string[];
    @jsonMember public groupName!: string;
    @jsonMember public groupId!: string;
    @jsonMember public link!: string;
    @jsonArrayMember(DanceInstance) public instances!: DanceInstance[];
}

@jsonObject export class DanceStats {
    @jsonMember public danceId!: string;
    @jsonMember public danceName!: string;
    @jsonMember public blogTag!: string;
    @jsonMember public seoName!: string;
    @jsonMember public description!: string;
    @jsonMember public songCount!: number;
    @jsonMember public songCountExplicit!: number;
    @jsonMember public songCountImplicit!: number;
    @jsonMember public maxWeight!: number;
    @jsonMember({constructor: DanceType}) public danceType!: DanceType | null;
    @jsonMember({constructor: DanceGroup}) public danceGroup!: DanceGroup | null;
    @jsonArrayMember(DanceStats) public children!: DanceStats[];

    public get tempoRange(): TempoRange {
        const numerator = this.danceType!.meter.numerator;
        const range = this.danceType!.tempoRange;

        return new TempoRange(numerator * range.min, numerator * range.max);
    }

    public filterByTempo(beatsPerMinute: number, beatsPerMeasure: number, percentEpsilon: number): boolean {

        if (!this.isCompatibleMeter(beatsPerMeasure)) {return false; }

        return this.isCompatibleTempo(beatsPerMinute, beatsPerMeasure, percentEpsilon);
    }

    public isCompatibleTempo(beatsPerMinute: number, beatsPerMeasure: number, percentEpsilon: number): boolean {
        const range = this.getFuzzyTempoRange(percentEpsilon);

        return (beatsPerMinute >= range.min) && (beatsPerMinute <= range.max);
    }

    public isCompatibleMeter(beatsPerMeasure: number): boolean {
        if (!beatsPerMeasure || beatsPerMeasure === 1) { return true; }

        const numerator = this.danceType!.meter.numerator;
        if (beatsPerMeasure === numerator) { return true; }

        //  2/4 and 4/4 are basically compatible meters as far as most dancing is concerned
        return (beatsPerMeasure === 2 && numerator === 4) || (beatsPerMeasure === 4 && numerator === 2);
    }

    public getFuzzyTempoRange(percentEpsilon: number): TempoRange {
        const range = this.tempoRange;
        const average = (range.min + range.max) / 2;
        const epsBpm = percentEpsilon * (average  / 100);

        return new TempoRange(range.min - epsBpm, range.max + epsBpm);
    }
}
