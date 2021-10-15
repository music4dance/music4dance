import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class DanceLink {
  @jsonMember public id!: string;
  @jsonMember public danceId!: string;
  @jsonMember public description!: string;
  @jsonMember public link!: string;

  public constructor(init?: Partial<DanceLink>) {
    Object.assign(this, init);
  }

  public cloneAndModify(changes: Partial<DanceLink>): DanceLink {
    const clone = new DanceLink(this);
    Object.assign(clone, changes);
    return clone;
  }
}
