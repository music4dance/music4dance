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
  @jsonMember(String, { name: "songTags" }) public songTagString!: string;
  @jsonMember(String, { name: "danceTags" }) public danceTagString!: string;

  public get songTags(): Tag[] {
    return new TagList(this.songTagString).tags;
  }

  public get danceTags(): Tag[] {
    return new TagList(this.danceTagString).tags;
  }
}
