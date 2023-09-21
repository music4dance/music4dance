import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";
import { SongListModel } from "./SongListModel";

@jsonObject
export class HolidaySongListModel extends SongListModel {
  @jsonMember public occassion!: string;
  @jsonMember public description!: string;
  @jsonMember public dance?: string;
  @jsonMember public playListId?: string;
}
