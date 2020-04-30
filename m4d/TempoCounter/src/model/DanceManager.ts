import 'reflect-metadata';
import { TypedJSON } from 'typedjson';
import { DanceStats, TempoRange } from './DanceStats';

import danceJson from '../assets/danceStats.json';
const danceStats = TypedJSON.parseAsArray(danceJson, DanceStats);
let loaded: DanceStats[] | undefined;

function FetchStats(): DanceStats[] {
    if (!loaded) {
        let statsString = danceJson;
        if  ((window as any).statsString) {
            statsString = (window as any).statsString.tree;
        }
        loaded = TypedJSON.parseAsArray(statsString, DanceStats);
    }
    return loaded;
}

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
        return this.rangeMpm.format + ' MPM (' + this.numerator + '/4)';
    }

    public get rangeBpmFormatted() {
        return this.rangeBpm.format + ' BPM';
    }

    private get numerator() {
        return this.dance.danceType!.meter.numerator;
    }

    private computeDelta(target: number): number {
        return this.dance.tempoRange.computeDelta(target);
    }
}

export function dancesForTempo(
    beatsPerMinute: number, beatsPerMeasure: number, percentEpsilon: number = 5): DanceOrder[] {

    // return danceStats.flatMap((group: DanceStats) => group.children);  TODO: See if we can find a general polyfill

    const stats = FetchStats();

    return stats
        .map((group: DanceStats) => group.children)
        .reduce((acc: DanceStats[], val: DanceStats[]) =>
            acc.concat(val))
            .filter((d: DanceStats) => d.filterByTempo(beatsPerMinute, beatsPerMeasure, percentEpsilon), [])
            .map((d: DanceStats)  => new DanceOrder(d, beatsPerMinute))
            .sort((a: DanceOrder, b: DanceOrder) => Math.abs(a.bpmDelta) - Math.abs(b.bpmDelta));
}


