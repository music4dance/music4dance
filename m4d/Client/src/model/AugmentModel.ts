import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class AugmentModel {
  @jsonMember public title?: string;
  @jsonMember public artist?: string;
  @jsonMember public id?: string;
}
