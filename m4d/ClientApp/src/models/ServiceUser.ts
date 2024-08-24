import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { SimplePlaylist } from "./SimplePlaylist";

@jsonObject
export class ServiceUser {
  @jsonMember(String) public id!: string;
  @jsonMember(String) public name!: string;
  @jsonArrayMember(SimplePlaylist) public playlists?: SimplePlaylist[];
}
