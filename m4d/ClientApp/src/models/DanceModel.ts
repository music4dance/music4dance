import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { DanceLink } from "./DanceLink";
import { SongListModel } from "./SongListModel";
import { Tag } from "./Tag";
import { TagList } from "./TagList";

@jsonObject
export class DanceModel extends SongListModel {
  @jsonMember(String) public danceId!: string;
  @jsonMember(String) public danceName!: string;
  @jsonMember(String) public description!: string;
  @jsonMember(String) public spotifyPlaylist!: string;
  @jsonArrayMember(DanceLink) public links!: DanceLink[];
  @jsonMember(String) public songTags!: string;

  public get tags(): Tag[] {
    return new TagList(this.songTags).tags;
  }
}
