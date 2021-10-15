import "reflect-metadata";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
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
