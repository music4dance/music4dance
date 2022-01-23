import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class UserComment {
  @jsonMember public userName!: string;
  @jsonMember public comment!: string;

  public constructor(init?: Partial<UserComment>) {
    Object.assign(this, init);
  }
}
