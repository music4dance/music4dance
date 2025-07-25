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

  @jsonMember(SongFilter) public filter!: SongFilter;
  @jsonArrayMember(SongHistory) public histories?: SongHistory[];
  @jsonMember(Number) public count!: number;
  @jsonMember(Number) public rawCount!: number;
  @jsonArrayMember(String) public hiddenColumns!: string[];
}
