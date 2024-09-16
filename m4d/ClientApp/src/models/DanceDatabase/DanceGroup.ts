import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { NamedObject } from "./NamedObject";
import type { DanceType } from "./DanceType";
import { assign } from "@/helpers/ObjectHelpers";

@jsonObject
export class DanceGroup extends NamedObject {
  @jsonMember(String) blogTag?: string;
  @jsonArrayMember(String) public danceIds!: string[];
  public dances: DanceType[] = [];

  // TODO: Lookes like TypedJSON doesn't hydrate the object
  //  in a way that instanceof works, so we'll use a different method
  //   public static isGroup(obj: NamedObject): obj is DanceGroup {
  //     return obj instanceof DanceGroup;
  //   }

  public static isGroup(obj: NamedObject): obj is DanceGroup {
    if (!obj) {
      return false;
    }
    return (obj as DanceGroup).danceIds !== undefined;
  }

  public constructor(init?: Partial<DanceGroup>) {
    super();
    assign(this, init);
  }
}
