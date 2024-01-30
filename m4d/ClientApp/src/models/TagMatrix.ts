import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import type { DanceDatabase } from "@/models/DanceDatabase/DanceDatabase";
import { DanceObject } from "@/models/DanceDatabase/DanceObject";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

const danceDatabase: DanceDatabase = safeDanceDatabase();

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
  public static create(danceId: string, counts: number[], isGroup: boolean): TagRow {
    const row = new TagRow();
    row.dance = danceDatabase.fromId(danceId)! as DanceObject;
    row.counts = counts;
    row.isGroup = isGroup;
    return row;
  }

  public dance!: DanceObject;
  public isGroup!: boolean;
}

@jsonObject
export class TagRowType extends TagRowBase {
  @jsonMember(String, { name: "dance" }) public danceId!: string;

  public get tagRow(): TagRow {
    return TagRow.create(this.danceId, this.counts, false);
  }
}

@jsonObject
export class TagRowGroup extends TagRowBase {
  @jsonMember(String, { name: "dance" }) public danceId!: string;
  @jsonArrayMember(TagRowType) public children!: TagRowType[];

  public get tagRow(): TagRow {
    return TagRow.create(this.danceId, this.counts, true);
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
