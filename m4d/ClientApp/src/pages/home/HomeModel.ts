import { jsonArrayMember, jsonObject } from "typedjson";
import { DanceClass } from "./DanceClass";
import { SiteMapEntry } from "@/models/SiteMapInfo";

@jsonObject
export class HomeModel {
  @jsonArrayMember(SiteMapEntry) public blogEntries?: SiteMapEntry[];
  @jsonArrayMember(DanceClass) public dances?: DanceClass[];
}
