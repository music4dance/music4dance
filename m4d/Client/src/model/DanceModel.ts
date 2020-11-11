import { jsonObject, jsonMember } from "typedjson";
import { SongListModel } from "./SongListModel";

@jsonObject
export class DanceModel extends SongListModel {
  @jsonMember public danceId!: string;
  @jsonMember public danceName!: string;
  @jsonMember public description!: string;
}
