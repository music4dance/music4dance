import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

@jsonObject
export class ArtistIndexEntry {
  @jsonMember(String) public name!: string;
  @jsonMember(Number) public songs!: number;
}

@jsonObject
export class ArtistIndexBucket {
  @jsonMember(String) public bucket!: string;
  @jsonMember(Number) public artists!: number;
}

/** Server model for /song/artists - see m4d/ViewModels/ArtistIndexModel.cs */
@jsonObject
export class ArtistIndexModel {
  @jsonMember(String) public letter?: string;
  @jsonMember(String) public query?: string;
  @jsonMember(Number) public minSongs!: number;
  @jsonMember(Boolean) public building?: boolean;
  @jsonMember(Number) public totalArtists!: number;
  @jsonMember(Date) public built?: Date;
  @jsonArrayMember(ArtistIndexBucket) public buckets!: ArtistIndexBucket[];
  @jsonArrayMember(ArtistIndexEntry) public artists!: ArtistIndexEntry[];
}
