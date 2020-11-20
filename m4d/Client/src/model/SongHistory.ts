import "reflect-metadata";
import { jsonMember, jsonObject, jsonArrayMember } from "typedjson";
import { SongProperty } from "./SongProperty";

@jsonObject
export class SongHistory {
  @jsonMember public id!: string; // GUID
  @jsonArrayMember(SongProperty) public properties!: SongProperty[];
}
