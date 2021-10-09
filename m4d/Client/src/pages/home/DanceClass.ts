import "reflect-metadata";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

@jsonObject
export class DanceMapping {
  @jsonMember public name!: string;
  @jsonMember public title!: string;
  @jsonMember public controller!: string;
  @jsonMember public queryString?: string;

  public get link(): string {
    const query = this.queryString;
    const path = query ? `?${query}` : `/${this.name}`;
    return `/${this.controller}${path}`;
  }
}

@jsonObject
export class DanceClass {
  @jsonMember public title!: string;
  @jsonMember public fullTitle!: string;
  @jsonMember public image!: string;
  @jsonMember public topDance?: string;
  @jsonArrayMember(DanceMapping) public dances!: DanceMapping[];
}
