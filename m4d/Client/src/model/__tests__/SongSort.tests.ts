import { SongSort, SortOrder } from '../SongSort';

describe('song sort', () => {
    it ('should handle empty', () => {
        const ss = new SongSort();
        expect(ss).toBeDefined();
        expect(ss).toBeInstanceOf(SongSort);
        expect(ss.order).toBeUndefined();
        expect(ss.direction).toEqual('asc');
        expect(ss.query).toEqual('');
        expect(ss.description).toEqual('');
    });

    it ('should handle ascending', () => {
        const ss = new SongSort('Tempo_asc');
        expect(ss).toBeDefined();
        expect(ss).toBeInstanceOf(SongSort);
        expect(ss.order).toEqual(SortOrder.Tempo);
        expect(ss.direction).toEqual('asc');
        expect(ss.query).toEqual('Tempo');
        expect(ss.description).toEqual('sorted by Tempo from slowest to fastest');
    });

    it ('should handle ascending', () => {
        const ss = new SongSort('Dances_desc');
        expect(ss).toBeDefined();
        expect(ss).toBeInstanceOf(SongSort);
        expect(ss.order).toEqual(SortOrder.Dances);
        expect(ss.direction).toEqual('desc');
        expect(ss.query).toEqual('Dances_desc');
        expect(ss.description).toEqual('sorted by Dance Rating from least popular to most popular');
    });

    it ('should handle switching direction from asc to desc', () => {
        const ss = new SongSort('Dances').change('Dances');
        expect(ss).toBeDefined();
        expect(ss).toBeInstanceOf(SongSort);
        expect(ss.order).toEqual(SortOrder.Dances);
        expect(ss.direction).toEqual('desc');
        expect(ss.query).toEqual('Dances_desc');
    });

    it ('should handle switching direction from desc to asc', () => {
        const ss = new SongSort('Dances_desc').change('Dances');
        expect(ss).toBeDefined();
        expect(ss).toBeInstanceOf(SongSort);
        expect(ss.order).toEqual(SortOrder.Dances);
        expect(ss.direction).toEqual('asc');
        expect(ss.query).toEqual('Dances');
    });
});

