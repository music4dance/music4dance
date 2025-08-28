import { jsonMember, jsonObject } from "typedjson";
import type { NamedObject } from "./DanceDatabase/NamedObject";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";

@jsonObject
export class DanceQueryItem {
  @jsonMember(String) public id!: string;
  @jsonMember(Number) public threshold!: number;

  public static fromValue(value: string): DanceQueryItem {
    const regex = /^([a-zA-Z0-9]+)([+-]?)(\d*)$/;
    const match = value.match(regex);
    if (!match) {
      throw new Error(`Invalid value format: ${value}`);
    }

    const dance = safeDanceDatabase().fromId(match[1]);
    if (!dance) {
      throw new Error(`Couldn't find dance ${match[1]}`);
    }

    const weight = match[3] ? parseInt(match[3]) : 1;
    return new DanceQueryItem({
      id: dance.id,
      threshold: match[2] === "-" ? -weight : weight,
    });
  }

  public constructor(init?: Partial<DanceQueryItem>) {
    Object.assign(this, init);
  }

  public get dance(): NamedObject {
    return safeDanceDatabase().fromId(this.id)!;
  }

  public toString(): string {
    return `${this.id}${this.threshold !== 1 ? (this.threshold < 0 ? "-" : "+") + this.threshold : ""}`;
  }

  public get description(): string {
    return this.threshold === 1
      ? this.dance.name
      : `${this.dance.name} (with ${this.threshold > 0 ? "at least" : "at most"} ${Math.abs(this.threshold)} votes)`;
  }

  public get shortDescription(): string {
    return this.threshold === 1
      ? this.dance.name
      : `${this.dance.name} ${this.threshold > 0 ? ">=" : "<="} ${Math.abs(this.threshold)}`;
  }
}
