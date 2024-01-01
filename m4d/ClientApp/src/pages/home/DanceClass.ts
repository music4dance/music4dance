import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

@jsonObject
export class DanceMapping {
  @jsonMember(String) public name!: string;
  @jsonMember(String) public title!: string;
  @jsonMember(String) public controller!: string;
  @jsonMember(String) public queryString?: string;

  public get link(): string {
    const query = this.queryString;
    const path = query ? `?${query}` : `/${this.name}`;
    return `/${this.controller}${path}`;
  }
}

@jsonObject
export class DanceClass {
  @jsonMember(String) public title!: string;
  @jsonMember(String) public fullTitle!: string;
  @jsonMember(String) public image!: string;
  @jsonMember(String) public topDance?: string;
  @jsonArrayMember(DanceMapping) public dances!: DanceMapping[];
}
