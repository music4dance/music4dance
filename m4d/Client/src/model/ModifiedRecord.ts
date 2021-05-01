import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class ModifiedRecord {
  @jsonMember public userName!: string;
  @jsonMember public like?: boolean;
  @jsonMember public isPseudo!: boolean;

  public static fromValue(value: string): ModifiedRecord {
    const parts = value.split("|");
    return new ModifiedRecord({
      userName: parts[0],
      isPseudo: parts.length > 1 && parts[1] === "P" ? true : false,
    });
  }

  public constructor(init?: Partial<ModifiedRecord>) {
    Object.assign(this, init);
  }
}
