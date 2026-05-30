import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

@jsonObject
export class ActivityLogEntry {
  @jsonMember(Number) public id!: number;
  @jsonMember(String) public date!: string;
  @jsonMember(String) public userName?: string;
  @jsonMember(String) public action!: string;
  @jsonMember(String) public details?: string;
}

@jsonObject
export class ActivityLogPageModel {
  @jsonArrayMember(ActivityLogEntry) public entries!: ActivityLogEntry[];
  @jsonMember(Number) public page!: number;
  @jsonMember(Number) public totalPages!: number;
}
