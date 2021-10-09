import { DanceObject } from "@/model/DanceObject";
import { DanceType } from "@/model/DanceType";
import "reflect-metadata";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

@jsonObject
export class TagColumn {
  @jsonMember public title!: string;
  @jsonMember public tag!: string;
}

@jsonObject
export class TagRowBase {
  @jsonArrayMember(Number) public counts!: number[];
}

@jsonObject
export class TagRow extends TagRowBase {
  public static create(
    dance: DanceObject,
    counts: number[],
    isGroup: boolean
  ): TagRow {
    const row = new TagRow();
    row.dance = dance;
    row.counts = counts;
    row.isGroup = isGroup;
    return row;
  }

  @jsonMember public dance!: DanceObject;
  @jsonMember public isGroup!: boolean;
}

@jsonObject
export class TagRowType extends TagRowBase {
  @jsonMember public dance!: DanceType;

  public get tagRow(): TagRow {
    return TagRow.create(this.dance, this.counts, false);
  }
}

@jsonObject
export class TagRowGroup extends TagRowBase {
  @jsonMember public dance!: DanceObject;
  @jsonArrayMember(TagRowType) public children!: TagRowType[];

  public get tagRow(): TagRow {
    return TagRow.create(this.dance, this.counts, true);
  }
}

@jsonObject
export class TagMatrix {
  @jsonArrayMember(TagColumn) public columns!: TagColumn[];
  @jsonArrayMember(TagRowGroup) public groups!: TagRowGroup[];

  public get list(): TagRow[] {
    const list: TagRow[] = [];
    for (const group of this.groups) {
      list.push(group.tagRow);
      for (const row of group.children) {
        list.push(row.tagRow);
      }
    }
    return list;
  }
}
