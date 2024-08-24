import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class AugmentModel {
  @jsonMember(String) public title?: string;
  @jsonMember(String) public artist?: string;
  @jsonMember(String) public id?: string;
}
