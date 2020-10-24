import { jsonObject, jsonArrayMember, jsonMember } from "typedjson";
import { Song } from "./Song";
import { SongFilter } from "./SongFilter";

@jsonObject
export class SongListModel {
  @jsonMember public filter!: SongFilter;
  @jsonArrayMember(Song) public songs!: Song[];
  @jsonMember public userName!: string;
  @jsonMember public count!: number;
  @jsonMember public hideSort?: boolean;
  @jsonArrayMember(String) public hiddenColumns?: string[];
}
