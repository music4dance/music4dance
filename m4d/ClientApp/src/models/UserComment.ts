import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class UserComment {
  @jsonMember(String) public userName!: string;
  @jsonMember(String) public comment!: string;

  public constructor(init?: Partial<UserComment>) {
    Object.assign(this, init);
  }
}
