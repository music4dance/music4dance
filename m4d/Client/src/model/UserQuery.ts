const identity = 'me';

export class UserQuery {
    public static fromParts(parts?: string): UserQuery {
        if (!parts) {
            return new UserQuery('');
        }

        if (parts.length !== 2) {
            throw new Error('parts should be exactly 2 characters');
        }

        let prefix = '';
        if (parts[0] === 'I') {
            prefix = '+';
        } else if (parts[0] === 'X') {
            prefix = '-';
        } else {
            throw new Error('1st character of parts must be "I" or "X"');
        }

        let suffix = '';
        switch (parts[1]) {
            case 'L':
                suffix = 'l';
                break;
            case 'H':
                suffix = 'h';
                break;
            case 'T':
                suffix = 'a';
                break;
            default:
                throw new Error('2nd character of parts must be one of "L", "H", or "T"');
        }

        return new UserQuery(prefix + 'me|' + suffix);
    }

    private data: string;

    public constructor(query?: string) {
        this.data = this.normalize(query);
    }

    public get query(): string {
        return this.data;
    }

    public get parts(): string | null {
        if (!this.data) {
            return null;
        }

        let ret = this.data[0] === '-' ? 'X' : 'I';

        const parts = this.data.split('|');
        if (parts.length > 1) {
            switch (parts[1]) {
                case 'l':
                    ret += 'L';
                    break;
                case 'h':
                    ret += 'H';
                    break;
                default:
                    ret += 'T';
            }
        } else {
            ret += 'T';
        }

        return ret;
    }

    private normalize(query?: string): string {
        if (!query) {
            return '';
        }
        query = query.trim().toLowerCase();
        if (!query) {
            return '';
        }
        if (query === 'null') {
            return query;
        }

        if (query[0] !== '+' && query[0] !== '-') {
            query = '+' + query;
        }
        if (query.indexOf('|') === -1) {
            query += '|';
        }
        return query;
    }
}
