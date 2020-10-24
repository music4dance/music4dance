// tslint:disable: max-classes-per-file
import "reflect-metadata";
import { jsonMember, jsonObject, jsonArrayMember } from "typedjson";

@jsonObject
export class DanceMapping {
  @jsonMember public name!: string;
  @jsonMember public title!: string;
  @jsonMember public controller!: string;
  @jsonMember public queryString?: string;
}

@jsonObject
export class DanceClass {
  @jsonMember public title!: string;
  @jsonMember public fullTitle!: string;
  @jsonMember public image!: string;
  @jsonMember public topDance?: string;
  @jsonArrayMember(DanceMapping) public dances!: DanceMapping[];
}
