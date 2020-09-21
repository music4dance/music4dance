/* tslint:disable:max-classes-per-file */
export enum SortOrder {
    Title = 'Title',
    Artist = 'Artist',
    Dances = 'Dances',
    Tempo = 'Tempo',
    Modified = 'Modified',
    Created = 'Created',
    Energy = 'Energy',
    Mood = 'Mood',
    Beat = 'Beat',
    Match = '',
}

export class SongSort {
    public static fromParts(order: string | null, direction: string): SongSort {
        return order ? new SongSort(order + '_' + direction) : new SongSort();
    }

    private data: string;

    public constructor(query?: string) {
        this.data = this.normalize(query);
    }

    public get query(): string {
        return this.data;
    }

    public get order(): string | null {
        return this.data ? this.data.split('_')[0] : null;
    }

    public get direction(): string {
        return this.data && this.data.endsWith('_desc') ? 'desc' : 'asc';
    }

    public get friendlyName(): string {
        switch (this.order) {
            case SortOrder.Dances:
                return 'Dance Rating';
            case SortOrder.Modified:
                return 'Last Modified';
            case SortOrder.Created:
                return 'When Added';
            case null:
            case '':
                return 'Closest Match';
            default:
                return this.order;
        }
    }

    public get description(): string {
        return this.order
            ?  `sorted by ${this.friendlyName} from ${this.directionDescription}`
            : '';
    }

    private get directionDescription(): string {
        if (this.direction === 'asc') {
            switch (this.order) {
                case SortOrder.Tempo:
                    return 'slowest to fastest';
                case SortOrder.Modified:
                case SortOrder.Created:
                    return 'newest to oldest';
                case SortOrder.Dances:
                    return 'most popular to least popular';
                case SortOrder.Beat:
                    return 'weakest to strongest';
                case SortOrder.Mood:
                    return 'saddest to happiest';
                case SortOrder.Energy:
                    return 'lowest to highest';
                default:
                    return 'A to Z';
            }
        } else {
            switch (this.order) {
                case SortOrder.Tempo:
                    return 'fastest to slowest';
                case SortOrder.Modified:
                case SortOrder.Created:
                    return 'oldest to newest';
                case SortOrder.Dances:
                    return 'least popular to most popular';
                case SortOrder.Beat:
                    return 'strongest to weakest';
                case SortOrder.Mood:
                    return 'happiest to saddest';
                case SortOrder.Energy:
                    return 'highest to lowest';
                default:
                    return 'Z to A';
            }
        }
    }

    private normalize(query: string | undefined): string {
        if (!query) {
            return '';
        }

        const parts = query.split('_').map((p) => p.trim());
        if (parts.length === 2 && parts[1].toLowerCase() === 'desc') {
            return parts[0] + '_desc';
        }

        return parts[0];
    }
}
