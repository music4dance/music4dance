import { jsonMember, jsonObject } from "typedjson";
import { SongListModel } from "./SongListModel";

@jsonObject
export class CustomSearchModel extends SongListModel {
  @jsonMember(String) public name!: string;
  @jsonMember(String) public description!: string;
  @jsonMember(String) public dance?: string;
  @jsonMember(String) public playListId?: string;
}
