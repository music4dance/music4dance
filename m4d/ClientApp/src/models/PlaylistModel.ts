import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { TrackModel } from "./TrackModel";

@jsonObject
export class PlaylistModel {
  @jsonMember(String) public name!: string;
  @jsonMember(String) public description!: string;
  @jsonArrayMember(TrackModel) public tracks?: TrackModel[];
}
