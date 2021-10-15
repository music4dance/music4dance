import "reflect-metadata";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { SongFilter } from "./SongFilter";
import { SongHistory } from "./SongHistory";

@jsonObject
export class SongListModel {
  public constructor(init?: Partial<SongListModel>) {
    Object.assign(this, init);
    if (!init?.filter) {
      this.filter = new SongFilter();
    }
    if (init && !init.count && init.histories) {
      this.count = init?.histories.length;
    }
  }

  @jsonMember public filter!: SongFilter;
  @jsonArrayMember(SongHistory) public histories?: SongHistory[];
  @jsonMember public count!: number;
}
