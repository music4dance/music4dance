import { DanceType } from "./DanceType";
import { DanceGroup } from "./DanceGroup";
import { TypedJSON, jsonArrayMember, jsonObject } from "typedjson";
import type { NamedObject } from "./NamedObject";

TypedJSON.setGlobalConfig({
  errorHandler: (e) => {
    // eslint-disable-next-line no-console
    console.error(e);
    throw e;
  },
});

@jsonObject({ onDeserialized: "onDeserialized" })
export class DanceDatabase {
  @jsonArrayMember(DanceType) dances!: DanceType[];
  @jsonArrayMember(DanceGroup) groups!: DanceGroup[];

  public constructor(init?: Partial<DanceDatabase>) {
    Object.assign(this, init);
  }

  public static load(json: string): DanceDatabase {
    const ret = TypedJSON.parse(json, DanceDatabase);
    if (!ret) {
      throw new Error("Failed to parse DanceDatabase");
    }
    return ret;
  }

  public get all(): NamedObject[] {
    return [...this.dances, ...this.groups];
  }

  public get flattened(): NamedObject[] {
    return this.groups.reduce((acc, x) => [...acc, x, ...x.dances], [] as NamedObject[]);
  }

  public filterByName(name: string): NamedObject[] {
    const n = name.toLowerCase();
    return this.flattened.filter((x) => x.name.toLowerCase().includes(n));
  }

  public fromId(id: string): NamedObject | undefined {
    return this.all.find((x) => x.id === id);
  }

  private onDeserialized(): void {
    for (const group of this.groups) {
      group.dances = group.danceIds.map((id) => this.dances.find((d) => d.id === id)!);
    }
  }
}
