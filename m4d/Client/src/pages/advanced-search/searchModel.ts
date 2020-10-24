/* tslint:disable:max-classes-per-file */
import "reflect-metadata";
import { jsonObject, jsonArrayMember, jsonMember } from "typedjson";
import { DanceObject } from "@/model/DanceStats";
import { Tag } from "@/model/Tag";
import { SongFilter } from "@/model/SongFilter";

@jsonObject
export class SearchModel {
  @jsonArrayMember(DanceObject) public dances!: DanceObject[];
  @jsonArrayMember(Tag) public tags!: Tag[];
  @jsonMember public filter!: SongFilter;
}
