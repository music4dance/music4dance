import "reflect-metadata";
import { jsonMember, jsonObject, jsonArrayMember } from "typedjson";
import { SongProperty } from "./SongProperty";
import { v4 as uuidv4 } from "uuid";

@jsonObject
export class SongHistory {
  @jsonMember public id!: string; // GUID
  @jsonArrayMember(SongProperty) public properties!: SongProperty[];

  public constructor(init?: Partial<SongHistory>) {
    this.id = init?.id ?? uuidv4();
    this.properties = init?.properties ?? [];
  }
}
