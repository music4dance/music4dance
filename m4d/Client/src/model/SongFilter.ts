import 'reflect-metadata';
import { jsonMember, jsonObject, jsonArrayMember} from 'typedjson';
import { DanceQuery } from './DanceQuery';

const subChar = '\u001a';
const scRegEx = new RegExp(subChar, 'g');

@jsonObject export class SongFilter {
    public static buildFilter(input: string): SongFilter {
        const filter = new SongFilter();

        const cells = SongFilter.splitFilter(input);

        filter.action = SongFilter.readCell(cells, 0);
        filter.dances = SongFilter.readCell(cells, 1);
        filter.sortOrder = SongFilter.readCell(cells, 2);
        filter.searchString = SongFilter.readCell(cells, 3);
        filter.purchase = SongFilter.readCell(cells, 4);
        filter.user = SongFilter. readCell(cells, 5);
        filter.tempoMin = SongFilter.readNumberCell(cells, 6);
        filter.tempoMax = SongFilter.readNumberCell(cells, 7);
        // Page
        filter.tags = SongFilter.readCell(cells, 9);
        filter.level = SongFilter.readNumberCell(cells, 10);

        return filter;
    }

    private static splitFilter(input: string): string[] {
        return input
            .replace(/\\-/g, subChar)
            .split('-').map((s) => s.trim()
            .replace(scRegEx, '-'));
    }

    private static readCell(cells: string[], index: number): string | undefined {
        return (cells.length >= index && cells[index] && cells[index] !== '.') ?
            cells[index] : undefined;
    }

    private static readNumberCell(cells: string[], index: number): number | undefined {
        const val =  (cells.length >= index && cells[index] && cells[index] !== '.') ?
            cells[index] : undefined;

        return val ? Number.parseFloat(val) : undefined;
    }


    @jsonMember public action?: string;
    @jsonMember public searchString?: string;
    @jsonMember public dances?: string;
    @jsonMember public sortOrder?: string;
    @jsonMember public purchase?: string;
    @jsonMember public user?: string;
    @jsonMember public tempoMin?: number;
    @jsonMember public tempoMax?: number;
    @jsonMember public tags?: string;
    @jsonMember public level?: number;

    public get query(): string {
        const danceQuery = new DanceQuery(this.dances);
        const tempoMin = this.tempoMin ? this.tempoMin.toString() : '';
        const tempoMax = this.tempoMax ? this.tempoMax.toString() : '';
        const level = this.level ? this.level : '';

        const ret = `${this.action}-${danceQuery.query}-${this.sortOrder}-${this.encode(this.searchString)}-` +
        `${this.purchase}-${this.user}-${tempoMin}-${tempoMax}--${this.encode(this.tags)}-${level}`;

        return this.trimEnd(ret, '.-');
    }

    private encode(s: string | undefined): string {
        const ret = s ? s.replace(/-/g, '\\-') : '';
        // // tslint:disable-next-line:no-console
        // console.log(ret);
        return ret;
    }

    private trimEnd(s: string, charlist: string): string {
        return s.replace(new RegExp('[' + charlist + ']+$'), '');
    }
}
