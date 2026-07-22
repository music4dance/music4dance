import { jsonMember, jsonObject } from "typedjson";
import type { NamedObject } from "./DanceDatabase/NamedObject";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { TagQuery } from "./TagQuery";

const primaryMarker = "*";

@jsonObject
export class DanceQueryItem {
  @jsonMember(String) public id!: string;
  @jsonMember(Number) public threshold!: number;
  @jsonMember(String) public tags?: string;
  // Marks this dance as the explicit target for dance-rating sort, tempo sort, and the
  // tempo range filter when more than one dance is selected - see DanceQuery.primaryDanceId.
  @jsonMember(Boolean) public primary?: boolean;

  public static fromValue(value: string): DanceQueryItem {
    const regex = /^([a-zA-Z0-9]+)(\*?)([+-]?)(\d*)\|?(.*)?$/;
    const match = value.match(regex);
    if (!match) {
      throw new Error(`Invalid value format: ${value}`);
    }

    const dance = safeDanceDatabase().fromId(match[1] || "");
    if (!dance) {
      throw new Error(`Couldn't find dance ${match[1]}`);
    }

    const weight = match[4] ? parseInt(match[4]) : 1;
    const tags = match[5] ?? undefined;

    return new DanceQueryItem({
      id: dance.id,
      primary: match[2] === primaryMarker ? true : undefined,
      threshold: match[3] === "-" ? -weight : weight,
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

  public get value(): string {
    return this.toString();
  }

  public toString(): string {
    const baseStr = `${this.id}${this.primary ? primaryMarker : ""}${this.threshold !== 1 ? (this.threshold < 0 ? "-" : "+") + Math.abs(this.threshold) : ""}`;
    if (this.tags && this.tags.length > 0) {
      return `${baseStr}|${this.tags}`;
    }
    return baseStr;
  }

  public get description(): string {
    const modifiers = [];
    if (this.threshold !== 1) {
      modifiers.push(
        this.threshold > 0 ? `at least ${this.threshold}` : `at most ${Math.abs(this.threshold)}`,
      );
    }
    if (this.tagQuery?.hasTags) {
      modifiers.push(this.tagQuery.description);
    }
    let desc = this.dance.name;
    if (modifiers.length > 0) {
      desc = `${desc} (${modifiers.join(", ")})`;
    }
    return desc;
  }

  public get shortDescription(): string {
    const modifiers = [];
    if (this.threshold !== 1) {
      modifiers.push(`${this.threshold > 0 ? ">=" : "<="}${Math.abs(this.threshold)}`);
    }
    if (this.tagQuery?.hasTags) {
      modifiers.push(this.tagQuery.shortDescription);
    }
    let desc = this.dance.name;
    if (modifiers.length > 0) {
      desc = `${desc} (${modifiers.join(", ")})`;
    }
    return desc;
  }
}
