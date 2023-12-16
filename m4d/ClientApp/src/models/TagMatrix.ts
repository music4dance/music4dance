import { DanceObject } from "@/models/DanceObject";
import { DanceType } from "@/models/DanceType";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

@jsonObject
export class TagColumn {
  @jsonMember(String) public title!: string;
  @jsonMember(String) public tag!: string;
}

@jsonObject
export class TagRowBase {
  @jsonArrayMember(Number) public counts!: number[];
}

@jsonObject
export class TagRow extends TagRowBase {
  public static create(dance: DanceObject, counts: number[], isGroup: boolean): TagRow {
    const row = new TagRow();
    row.dance = dance;
    row.counts = counts;
    row.isGroup = isGroup;
    return row;
  }

  @jsonMember(DanceObject) public dance!: DanceObject;
  @jsonMember(Boolean) public isGroup!: boolean;
}

@jsonObject
export class TagRowType extends TagRowBase {
  @jsonMember(DanceType) public dance!: DanceType;

  public get tagRow(): TagRow {
    return TagRow.create(this.dance, this.counts, false);
  }
}

@jsonObject
export class TagRowGroup extends TagRowBase {
  @jsonMember(DanceObject) public dance!: DanceObject;
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
