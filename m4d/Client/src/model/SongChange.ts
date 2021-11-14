import { PropertyType, SongProperty } from "./SongProperty";
import { UserQuery } from "./UserQuery";

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

  public get userName(): string | undefined {
    const query = this.userQuery;
    return query ? query.userName : undefined;
  }

  public get userDisplayName(): string | undefined {
    const query = this.userQuery;
    return query ? query.displayName : undefined;
  }

  public get like(): boolean | undefined {
    const likes = this.properties.filter(
      (p) => p.baseName === PropertyType.likeTag
    );
    return likes.length ? (likes.pop()?.valueTyped as boolean) : undefined;
  }

  private get userQuery(): UserQuery | undefined {
    const user = this.user;
    if (!user) {
      return undefined;
    }
    return new UserQuery(user);
  }
}
