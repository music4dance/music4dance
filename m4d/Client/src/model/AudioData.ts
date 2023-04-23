import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class AudioData {
  @jsonMember public beatsPerMeasure?: number;
  @jsonMember public beatsPerMinute?: number;
  @jsonMember public danceability?: number;
  @jsonMember public energy?: number;
  @jsonMember public valence?: number;
}
