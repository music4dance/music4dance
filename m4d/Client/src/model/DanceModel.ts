import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { DanceLink } from "./DanceLink";
import { SongListModel } from "./SongListModel";

@jsonObject
export class DanceModel extends SongListModel {
  @jsonMember public danceId!: string;
  @jsonMember public danceName!: string;
  @jsonMember public description!: string;
  @jsonMember public spotifyPlaylist!: string;
  @jsonArrayMember(DanceLink) public links!: DanceLink[];
}
