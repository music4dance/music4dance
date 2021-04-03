import "reflect-metadata";
import { jsonObject, jsonArrayMember, jsonMember } from "typedjson";
import { DanceObject } from "@/model/DanceStats";
import { SongFilter } from "@/model/SongFilter";

@jsonObject
export class SearchModel {
  @jsonArrayMember(DanceObject) public dances!: DanceObject[];
  @jsonMember public filter!: SongFilter;
}
