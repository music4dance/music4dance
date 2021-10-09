import { jsonMember, jsonObject } from "typedjson";
import { SongListModel } from "./SongListModel";

@jsonObject
export class AlbumModel extends SongListModel {
  @jsonMember public title!: string;
  @jsonMember public artist?: string;
}
