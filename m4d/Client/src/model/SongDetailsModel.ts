import { jsonMember, jsonObject } from "typedjson";
import { SongFilter } from "./SongFilter";
import { SongHistory } from "./SongHistory";

@jsonObject
export class SongDetailsModel {
  @jsonMember public created?: boolean;
  @jsonMember public songHistory!: SongHistory;
  @jsonMember public filter!: SongFilter;
  @jsonMember public userName!: string;

  public constructor(init?: Partial<SongDetailsModel>) {
    Object.assign(this, init);
  }
}
