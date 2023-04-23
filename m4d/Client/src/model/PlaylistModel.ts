import "reflect-metadata";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { TrackModel } from "./TrackModel";

@jsonObject
export class PlaylistModel {
  @jsonMember public name!: string;
  @jsonMember public description!: string;
  @jsonArrayMember(TrackModel) public tracks?: TrackModel[];
}
