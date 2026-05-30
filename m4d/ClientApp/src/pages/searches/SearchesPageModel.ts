import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

@jsonObject
export class SearchSummary {
  @jsonMember(Number) public id!: number;
  @jsonMember(String) public userName!: string;
  @jsonMember(String) public query?: string;
  @jsonMember(String) public description?: string;
  @jsonMember(String) public searchUrl!: string;
  @jsonMember(String) public searchPageUrl?: string;
  @jsonMember(Number) public mostRecentPage?: number;
  @jsonMember(Number) public count!: number;
  @jsonMember(String) public created!: string;
  @jsonMember(String) public modified!: string;
  @jsonMember(String) public spotify?: string;
  @jsonMember(String) public deleteUrl!: string;
}

@jsonObject
export class SearchesPageModel {
  @jsonArrayMember(SearchSummary) public searches!: SearchSummary[];
  @jsonMember(Number) public page!: number;
  @jsonMember(Number) public totalPages!: number;
  @jsonMember(String) public sort?: string;
  @jsonMember(Boolean) public showDetails!: boolean;
  @jsonMember(Boolean) public spotifyOnly!: boolean;
  @jsonMember(String) public user?: string;
  @jsonMember(Boolean) public isAdmin!: boolean;
  @jsonMember(Boolean) public canDeleteAll!: boolean;
  @jsonMember(String) public basicSearchUrl!: string;
  @jsonMember(String) public advancedSearchUrl!: string;
  @jsonMember(String) public deleteAllUrl?: string;
}
