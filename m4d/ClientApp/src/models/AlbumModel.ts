import { jsonMember, jsonObject } from "typedjson";
import { SongListModel } from "./SongListModel";

@jsonObject
export class AlbumModel extends SongListModel {
  @jsonMember(String) public title!: string;
  @jsonMember(String) public artist?: string;
}
