export class DanceRatingDelta {
    public static fromString(value: string): DanceRatingDelta {
        if (!value) {
            throw new Error('value must not be falsey');
        }

        const groups = /([A-Z]+)([+-]\d+)/g.exec(value);

        if (groups?.length !== 3) {
            throw new Error('value must conform to {dancid}(+|-){count}');
        }

        return new DanceRatingDelta(groups[1], Number.parseInt(groups[2], 10));
    }

    constructor(readonly danceId: string, readonly delta: number) {}

    public toString(): string {
        return `${this.danceId}${this.delta < 0 ? '-' : '+'}${Math.abs(this.delta)}`;
    }
}
