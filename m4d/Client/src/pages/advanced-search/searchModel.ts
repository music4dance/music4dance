import { DanceObject } from "@/model/DanceObject";
import { SongFilter } from "@/model/SongFilter";
import "reflect-metadata";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

@jsonObject
export class SearchModel {
  @jsonArrayMember(DanceObject) public dances!: DanceObject[];
  @jsonMember public filter!: SongFilter;
}
