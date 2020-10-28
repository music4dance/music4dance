import { jsonObject, jsonMember } from "typedjson";
import { SongListModel } from "./SongListModel";

@jsonObject
export class HolidaySongListModel extends SongListModel {
  @jsonMember public dance?: string;
  @jsonMember public playListId?: string;
}
