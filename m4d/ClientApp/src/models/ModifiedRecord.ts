import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class ModifiedRecord {
  @jsonMember(String) public userName!: string;
  @jsonMember(Boolean) public like?: boolean;
  @jsonMember(Boolean) public isPseudo!: boolean;
  @jsonMember(Boolean) public isCreator?: boolean;

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
