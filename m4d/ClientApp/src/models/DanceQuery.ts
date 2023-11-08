import { DanceQueryBase } from "./DanceQueryBase";

const all = "ALL";
const and = "AND"; // Exclusive + Explicit
const andX = "ADX"; // Exclusive + Inferred
// const oneOf:string  = ''; // Inclusive + Explicit
const oneOfX = "OOX"; // Inclusive + Inferred

const modifiers: string[] = [all, and, andX, oneOfX];

export class DanceQuery extends DanceQueryBase {
  public static fromParts(dances: string[], exclusive?: boolean): DanceQuery {
    if (!dances || dances.length === 0) {
      return new DanceQuery();
    }

    const modifier = exclusive ? and : "";
    const composite = modifier ? [modifier, ...dances] : dances;
    return new DanceQuery(composite.join(","));
  }

  private data: string;

  public constructor(query?: string) {
    super();
    this.data = query ? query : "";
    if (all === this.data.toUpperCase()) {
      this.data = "";
    }
  }

  public get query(): string {
    return this.data;
  }

  public get danceList(): string[] {
    const items = this.data
      .split(",")
      .map((s) => s.trim())
      .filter((s) => s);

    if (
      items.length > 0 &&
      modifiers.find((m) => m === items[0].toUpperCase())
    ) {
      items.shift();
    }

    return items;
  }

  public get isExclusive(): boolean {
    return this.startsWithAny([and, andX]) && this.data.indexOf(",", 4) !== -1;
  }

  public get description(): string {
    const prefix = this.isExclusive ? "all" : "any";
    const connector = this.isExclusive ? "and" : "or";
    const dances = this.danceNames;

    switch (dances.length) {
      case 0:
        return `songs`;
      case 1:
        return `${dances[0]} songs`;
      case 2:
        return `songs danceable to ${prefix} of ${dances[0]} ${connector} ${dances[1]}`;
      default: {
        const last = dances.pop();
        return `songs danceable to ${prefix} of ${dances.join(
          ", "
        )} ${connector} ${last}`;
      }
    }
    return "";
  }

  public get shortDescription(): string {
    return this.danceNames.join(", ");
  }

  private startsWithAny(qualifiers: string[]): boolean {
    return !!qualifiers.find((q) => this.startsWith(q));
  }

  private startsWith(qualifier: string) {
    return this.data.toUpperCase().startsWith(qualifier + ",");
  }
}
