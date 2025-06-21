import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { SongHistory } from "./SongHistory";

@jsonObject
export class PlaylistViewerModel {
  @jsonMember(String) public id!: string;
  @jsonArrayMember(SongHistory) public histories!: SongHistory[];
  @jsonMember(String) public name!: string;
  @jsonMember(String) public description?: string;
  @jsonMember(String) public ownerId!: string;
  @jsonMember(String) public ownerName!: string;
  @jsonMember(Number) public totalCount!: number;
}
