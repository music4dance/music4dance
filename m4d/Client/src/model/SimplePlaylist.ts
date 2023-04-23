import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class SimplePlaylist {
  @jsonMember public id!: string;
  @jsonMember public name?: string;
  @jsonMember public description?: string;
  @jsonMember public trackCount?: number;
  @jsonMember public owner?: string;
  @jsonMember public music4danceId?: string;
}
