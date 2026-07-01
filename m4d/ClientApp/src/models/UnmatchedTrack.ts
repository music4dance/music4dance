import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class UnmatchedTrack {
  @jsonMember(String) public title!: string;
  @jsonMember(String) public artist!: string;
  @jsonMember(String) public trackId!: string;
}
