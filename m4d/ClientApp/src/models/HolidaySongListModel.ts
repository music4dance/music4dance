import { jsonMember, jsonObject } from "typedjson";
import { SongListModel } from "./SongListModel";

@jsonObject
export class HolidaySongListModel extends SongListModel {
  @jsonMember(String) public occassion!: string;
  @jsonMember(String) public description!: string;
  @jsonMember(String) public dance?: string;
  @jsonMember(String) public playListId?: string;
}
