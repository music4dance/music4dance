import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";
import { UserQuery } from "./UserQuery";

@jsonObject
export class ProfileModel {
  @jsonMember public userName!: string;
  @jsonMember public isPublic!: boolean;
  @jsonMember public isPseudo!: boolean;
  @jsonMember public spotifyId?: string;
  @jsonMember public favoriteCount?: number;
  @jsonMember public blockedCount?: number;
  @jsonMember public editCount?: number;

  public get displayName(): string {
    return new UserQuery(this.userName).displayName;
  }

  public get isAnonymous(): boolean {
    return new UserQuery(this.userName).isAnonymous;
  }
}
