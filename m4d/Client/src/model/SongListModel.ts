import { jsonObject, jsonArrayMember, jsonMember } from "typedjson";
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
  @jsonMember public userName!: string;
  @jsonMember public count!: number;
  @jsonMember public hideSort?: boolean;
  @jsonArrayMember(String) public hiddenColumns?: string[];
}
