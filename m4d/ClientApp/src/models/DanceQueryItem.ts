import { jsonMember, jsonObject } from "typedjson";
import type { NamedObject } from "./DanceDatabase/NamedObject";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { TagQuery } from "./TagQuery";

@jsonObject
export class DanceQueryItem {
  @jsonMember(String) public id!: string;
  @jsonMember(Number) public threshold!: number;
  @jsonMember(String) public tags?: string;

  public static fromValue(value: string): DanceQueryItem {
    const regex = /^([a-zA-Z0-9]+)([+-]?)(\d*)\|?(.*)?$/;
    const match = value.match(regex);
    if (!match) {
      throw new Error(`Invalid value format: ${value}`);
    }

    const dance = safeDanceDatabase().fromId(match[1]);
    if (!dance) {
      throw new Error(`Couldn't find dance ${match[1]}`);
    }

    const weight = match[3] ? parseInt(match[3]) : 1;
    const tags = match[4] ?? undefined;

    return new DanceQueryItem({
      id: dance.id,
      threshold: match[2] === "-" ? -weight : weight,
      tags: tags ? tags : undefined,
    });
  }

  public constructor(init?: Partial<DanceQueryItem>) {
    Object.assign(this, init);
  }

  public get dance(): NamedObject {
    return safeDanceDatabase().fromId(this.id)!;
  }

  public get tagQuery(): TagQuery | undefined {
    if (this.tags && this.tags.length > 0) {
      return new TagQuery(this.tags);
    }
    return undefined;
  }

  public toString(): string {
    const baseStr = `${this.id}${this.threshold !== 1 ? (this.threshold < 0 ? "-" : "+") + Math.abs(this.threshold) : ""}`;
    if (this.tags && this.tags.length > 0) {
      return `${baseStr}|${this.tags}`;
    }
    return baseStr;
  }

  public get description(): string {
    let desc =
      this.threshold === 1
        ? this.dance.name
        : `${this.dance.name} (with ${this.threshold > 0 ? "at least" : "at most"} ${Math.abs(this.threshold)} votes)`;
    if (this.tagQuery?.hasTags) {
      desc = `${desc.substring(0, desc.length - 1)} ${this.tagQuery.description})`;
    }
    return desc;
  }

  public get shortDescription(): string {
    let desc =
      this.threshold === 1
        ? this.dance.name
        : `${this.dance.name} ${this.threshold > 0 ? ">=" : "<="} ${Math.abs(this.threshold)}`;
    if (this.tagQuery && this.tagQuery.tagList && this.tagQuery.tagList.tags.length > 0) {
      desc += ` (${this.tagQuery.shortDescription})`;
    }
    return desc;
  }
}
