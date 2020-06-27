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
