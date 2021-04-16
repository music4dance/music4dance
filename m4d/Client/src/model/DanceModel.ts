import { jsonObject, jsonMember, jsonArrayMember } from "typedjson";
import { DanceEnvironment } from "./DanceEnvironmet";
import { DanceLink } from "./DanceStats";
import { SongListModel } from "./SongListModel";

declare const environment: DanceEnvironment;

@jsonObject
export class DanceModel extends SongListModel {
  @jsonMember public danceId!: string;
  @jsonMember public danceName!: string;
  @jsonMember public description!: string;
  @jsonArrayMember(DanceLink) public links!: DanceLink[];
}
