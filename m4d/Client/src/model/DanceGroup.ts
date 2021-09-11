import { jsonMember, jsonObject, jsonArrayMember } from "typedjson";
import { NamedObject } from "./NamedObject";

@jsonObject
export class DanceGroup extends NamedObject {
  @jsonMember blogTag?: string;
  @jsonArrayMember(String) public danceIds!: string[];

  public constructor(init?: Partial<DanceGroup>) {
    super();
    Object.assign(this, init);
  }
}
