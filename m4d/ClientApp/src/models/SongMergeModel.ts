import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { SongHistory } from "./SongHistory";

@jsonObject
export class SongMergeModel {
  @jsonMember public songId!: string;
  @jsonArrayMember(SongHistory) public songs!: SongHistory[];

  public constructor(init?: Partial<SongMergeModel>) {
    Object.assign(this, init);
  }
}
