import { jsonObject, jsonArrayMember, jsonMember } from "typedjson";
import { Song } from "./Song";
import { SongFilter } from "./SongFilter";
import { SongHistory } from "./SongHistory";

@jsonObject
export class SongListModel {
  @jsonMember public filter!: SongFilter;
  @jsonArrayMember(Song) public songs!: Song[];
  @jsonArrayMember(SongHistory) public histories?: SongHistory[];
  @jsonMember public userName!: string;
  @jsonMember public count!: number;
  @jsonMember public hideSort?: boolean;
  @jsonArrayMember(String) public hiddenColumns?: string[];
  @jsonMember public validate?: boolean;

  public check(): void {
    if (!this.validate || !this.histories) {
      return;
    }

    let failed = false;
    for (let i = 0; i < this.songs.length; i++) {
      if (!this.songs[i].compareToHistory(this.histories[i], this.userName)) {
        failed = true;
      }
    }

    if (failed) {
      console.log("Failed to validate all songs on this page.");
    } else {
      console.log("Validation succeeded for all songs on this page");
    }
  }
}
