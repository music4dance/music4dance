import { DanceEnvironment } from './DanceEnvironmet';
import { DanceStats } from './DanceStats';

declare const environment: DanceEnvironment;

const all: string  = 'ALL';
const and: string = 'AND'; // Exclusive + Explicit
const andX: string = 'ADX'; // Exclusive + Inferred
// const oneOf:string  = ''; // Inclusive + Explicit
const oneOfX: string = 'OOX'; // Inclusive + Inferred

const modifiers: string[] = [all, and, andX, oneOfX];

export class DanceQuery {
    public static fromParts(dances: string[], exclusive?: boolean, inferred?: boolean): DanceQuery {
        if (!dances || dances.length === 0) {
            return new DanceQuery();
        }

        let modifier = '';
        if (exclusive) {
            if (inferred) {
                modifier = andX;
            } else {
                modifier = and;
            }
        } else if (inferred) {
            modifier = oneOfX;
        }

        const composite = modifier ? [modifier, ...dances] : dances;
        return new DanceQuery(composite.join(','));
    }

    private data: string;

    public constructor(query?: string) {
        this.data = query ? query : '';
        if (all === this.data.toUpperCase()) {
            this.data = '';
        }
    }

    public get query(): string {
        return this.data;
    }

    public get danceList(): string[] {
        const items = this.data.split(',').map((s) => s.trim()).filter((s) => s);

        if (items.length > 0 && modifiers.find((m) => m === items[0].toUpperCase())) {
            items.shift();
        }

        return items;
    }

    public get isExclusive(): boolean {
        return this.startsWithAny([and, andX]) && this.data.indexOf(',', 4) !== -1;
    }

    public get includeInferred(): boolean {
        return this.startsWithAny([andX, oneOfX]);
    }

    private get dances(): DanceStats[]  {
        return this.danceList.map((id) => environment!.fromId(id)!);
    }

    private get danceNames(): string[] {
        return this.dances.map((d) => d.danceName);
    }

    public get description(): string {
        const prefix = this.isExclusive ? 'all' : 'any';
        const connector = this.isExclusive ? 'and' : 'or';
        const suffix = this.includeInferred ? ' (including inferred by tempo)' : '';
        const dances = this.danceNames;

        switch (dances.length) {
            case 0:
                return `songs${suffix}`;
            case 1:
                return `${dances[0]} songs${suffix}`;
            case 2:
                return `songs danceable to ${prefix} of ${dances[0]} ${connector} ${dances[1]}${suffix}`;
            default:
                const last = dances.pop();
                return `songs danceable to ${prefix} of ${dances.join(', ')} ${connector} ${last}${suffix}`;
        }
        return '';
    }

    public get shortDescription(): string {
        return this.danceNames.join(', ');
    }

    public get singleDance(): boolean {
        return this.danceList.length === 1;
    }

    public get isSimple(): boolean {
        return this.danceList.length < 2;
    }

    private startsWithAny(qualifiers: string[]): boolean {
        return !!qualifiers.find((q) => this.startsWith(q));
    }

    private startsWith(qualifier: string) {
        return this.data.toUpperCase().startsWith(qualifier + ',');
    }
}
