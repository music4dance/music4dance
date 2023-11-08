import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { AudioData } from "./AudioData";
import { ServiceType } from "./Purchase";

export enum ServiceName {
  None = "None",
  Amazon = "Amazon",
  ITunes = "ITunes",
  Spotify = "Spotify",
  XBox = "XBox",
  Emusic = "Emusic",
  Pandora = "Pandora",
  AMG = "AMG",
  Max = "Max",
}

@jsonObject
export class TrackModel {
  @jsonMember(String) public service!: ServiceName;
  @jsonMember(String) public trackId!: string;
  @jsonMember(String) public name!: string;
  @jsonMember(String) public collectionId!: string;
  @jsonMember(String) public altId?: string;
  @jsonMember(String) public artist!: string;
  @jsonMember(String) public album?: string;
  @jsonMember(String) public imageUrl?: string;
  @jsonMember(String) public purchaseInfo?: string;
  @jsonMember(String) public releaseDate?: string;
  @jsonArrayMember(String) public genres?: string[];
  @jsonMember(Number) public duration?: number;
  @jsonMember(Number) public trackNumber?: number;
  @jsonMember(Number) public trackRank?: number;
  @jsonMember(Boolean) public isPlayable?: boolean;
  @jsonArrayMember(String) public availableMarkets?: string[];
  @jsonMember(String) public sampleUrl?: string;
  @jsonMember(AudioData) public audioData?: AudioData;

  public get serviceType(): ServiceType {
    return this.service[0].toLowerCase() as ServiceType;
  }
}

@jsonObject
export class EnhancedTrackModel extends TrackModel {
  @jsonMember(String) public songId?: string;
}
