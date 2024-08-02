import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class DanceLink {
  @jsonMember(String) public id!: string;
  @jsonMember(String) public danceId!: string;
  @jsonMember(String) public description!: string;
  @jsonMember(String) public link!: string;

  public constructor(init?: Partial<DanceLink>) {
    Object.assign(this, init);
  }

  public cloneAndModify(changes: Partial<DanceLink>): DanceLink {
    const clone = new DanceLink(this);
    Object.assign(clone, changes);
    return clone;
  }
}
