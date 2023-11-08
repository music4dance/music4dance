import { jsonMember, jsonObject } from "typedjson";
import { SongFilter } from "./SongFilter";
import { SongHistory } from "./SongHistory";

@jsonObject
export class SongDetailsModel {
  @jsonMember(Boolean) public created?: boolean;
  @jsonMember(SongHistory) public songHistory!: SongHistory;
  @jsonMember(SongFilter) public filter!: SongFilter;
  @jsonMember(String) public userName!: string;

  public constructor(init?: Partial<SongDetailsModel>) {
    Object.assign(this, init);
  }
}
