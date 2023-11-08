import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class SimplePlaylist {
  @jsonMember(String) public id!: string;
  @jsonMember(String) public name?: string;
  @jsonMember(String) public description?: string;
  @jsonMember(Number) public trackCount?: number;
  @jsonMember(String) public owner?: string;
  @jsonMember(String) public music4danceId?: string;
}
