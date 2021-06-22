import { PropertyType, SongProperty } from "./SongProperty";

export class SongChange {
    public constructor(
        public action: string,
        public properties: SongProperty[],
        public user?: string,
        public date?: Date
    ) {}

    public get isBatch(): boolean {
        const user = this.user;
        return !!user && (user === "batch|P" || user.startsWith("batch-"));
    }

    public get isPseudo(): boolean {
        const user = this.user;
        return !!user && user.endsWith("|P");
    }

    public get baseUser(): string | undefined {
        const user = this.user;
        if (!user) {
            return undefined;
        }
        const parts = user.split("|");
        return parts[0];
    }

    public get like(): boolean | undefined {
        const likes = this.properties.filter(
            (p) => p.baseName === PropertyType.likeTag
        );
        return likes.length ? (likes.pop()?.valueTyped as boolean) : undefined;
    }
}
