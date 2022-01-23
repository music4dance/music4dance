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
    return this.userQuery.isPseudo;
  }

  public get userName(): string | undefined {
    return this.userQuery.userName;
  }

  public get userDisplayName(): string | undefined {
    return this.userQuery.displayName;
  }

  public get like(): boolean | undefined {
    const likes = this.properties.filter(
      (p) => p.baseName === PropertyType.likeTag
    );
    return likes.length ? (likes.pop()?.valueTyped as boolean) : undefined;
  }

  private get userQuery(): UserQuery {
    return new UserQuery(this.user);
  }
}
