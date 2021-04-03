import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class ModifiedRecord {
  @jsonMember public userName!: string;
  @jsonMember public like?: boolean;

  public constructor(init?: Partial<ModifiedRecord>) {
    Object.assign(this, init);
  }
}
