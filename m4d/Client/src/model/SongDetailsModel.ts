import { jsonObject, jsonMember } from "typedjson";
import { Song } from "./Song";
import { SongFilter } from "./SongFilter";
import { SongHistory } from "./SongHistory";

@jsonObject
export class SongDetailsModel {
  @jsonMember public songHistory!: SongHistory;
  @jsonMember public filter!: SongFilter;
  @jsonMember public userName!: string;
  @jsonMember public song!: Song;
}
