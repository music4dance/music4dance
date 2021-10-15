import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class NamedObject {
  @jsonMember public id!: string;
  @jsonMember public name!: string;
  @jsonMember public description?: string;
}
