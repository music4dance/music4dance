import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

@jsonObject
export class PlayListSummary {
  @jsonMember(String) public id!: string;
  @jsonMember(String) public user!: string;
  @jsonMember(Number) public type!: number;
  @jsonMember(String) public name?: string;
  @jsonMember(String) public description?: string;
  @jsonMember(String) public data1?: string;
  @jsonMember(String) public data2?: string;
  @jsonMember(String) public created!: string;
  @jsonMember(String) public updated?: string;
  @jsonMember(Boolean) public deleted!: boolean;
}

@jsonObject
export class PlayListPageModel {
  @jsonArrayMember(PlayListSummary) public playLists!: PlayListSummary[];
  @jsonMember(Number) public type!: number;
  @jsonMember(String) public filteredUser?: string;
  @jsonMember(String) public data1Name!: string;
  @jsonMember(String) public data2Name!: string;
}
