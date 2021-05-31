import { jsonObject, jsonMember, jsonArrayMember } from "typedjson";
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
  @jsonMember public service!: ServiceName;
  @jsonMember public trackId!: string;
  @jsonMember public name!: string;
  @jsonMember public collectionId!: string;
  @jsonMember public altId?: string;
  @jsonMember public artist!: string;
  @jsonMember public album?: string;
  @jsonMember public imageUrl?: string;
  @jsonMember public purchaseInfo?: string;
  @jsonMember public releaseDate?: string;
  @jsonArrayMember(String) public genres?: string[];
  @jsonMember public duration?: number;
  @jsonMember public trackNumber?: number;
  @jsonMember public trackRank?: number;
  @jsonMember public isPlayable?: boolean;
  @jsonArrayMember(String) public availableMarkets?: string[];
  @jsonMember public sampleUrl?: string;

  public get serviceType() {
    return this.service[0].toLowerCase() as ServiceType;
  }
}
