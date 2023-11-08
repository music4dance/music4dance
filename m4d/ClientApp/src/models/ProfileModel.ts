import { jsonMember, jsonObject } from "typedjson";
import { UserQuery } from "./UserQuery";

@jsonObject
export class ProfileModel {
  @jsonMember(String) public userName!: string;
  @jsonMember(Boolean) public isPublic!: boolean;
  @jsonMember(Boolean) public isPseudo!: boolean;
  @jsonMember(String) public spotifyId?: string;
  @jsonMember(Number) public favoriteCount?: number;
  @jsonMember(Number) public blockedCount?: number;
  @jsonMember(Number) public editCount?: number;

  public get displayName(): string {
    return new UserQuery(this.userName).displayName;
  }

  public get isAnonymous(): boolean {
    return new UserQuery(this.userName).isAnonymous;
  }
}
