import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class AudioData {
  @jsonMember(Number) public beatsPerMeasure?: number;
  @jsonMember(Number) public beatsPerMinute?: number;
  @jsonMember(Number) public danceability?: number;
  @jsonMember(Number) public energy?: number;
  @jsonMember(Number) public valence?: number;
}
