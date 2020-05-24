import { fetchStats, dancesForTempo, getStyles, getTypes } from '../DanceManager';

describe('dance stats manager', () => {
    it('should load', () => {
        const loaded = fetchStats();

        expect(loaded).toBeDefined();
        expect(loaded.length).toBeDefined();
        expect(loaded.length).toEqual(7);
    });

    it('should filter tempos', () => {
        const hundred = dancesForTempo(100, 4);

        expect(hundred).toBeDefined();
        expect(hundred.length).toBeDefined();
        expect(hundred.length).toEqual(9);
    });

    it('should return unique styles', () => {
        const styles = getStyles();
        expect(styles).toBeDefined();
        expect(styles.length).toBeDefined();
        expect(styles.length).toEqual(6);
    });

    it('should return unique types', () => {
        const types = getTypes();
    });
});
