import "reflect-metadata";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { SimplePlaylist } from "./SimplePlaylist";

@jsonObject
export class ServiceUser {
  @jsonMember public id!: string;
  @jsonMember public name!: string;
  @jsonArrayMember(SimplePlaylist) public playlists?: SimplePlaylist[];
}
