const all: string  = 'ALL';
const and: string = 'AND'; // Exclusive + Explicit
const andX: string = 'ADX'; // Exclusive + Inferred
// const oneOf:string  = ''; // Inclusive + Explicit
const oneOfX: string = 'OOX'; // Inclusive + Inferred

const modifiers: string[] = [all, and, andX, oneOfX];

export class DanceQuery {
    public static fromParts(dances: string[], exclusive: boolean, inferred: boolean): DanceQuery {
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

    private startsWithAny(qualifiers: string[]): boolean {
        return !!qualifiers.find((q) => this.startsWith(q));
    }

    private startsWith(qualifier: string) {
        return this.data.toUpperCase().startsWith(qualifier + ',');
    }
}
