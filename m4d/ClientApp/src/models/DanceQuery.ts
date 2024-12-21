import { DanceQueryBase } from "./DanceQueryBase";
import { DanceThreshold } from "./DanceThreshold";

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

  public get danceThresholds(): DanceThreshold[] {
    const items = this.data
      .split(",")
      .map((s) => s.trim())
      .filter((s) => s);

    if (items.length > 0 && modifiers.find((m) => m === items[0].toUpperCase())) {
      items.shift();
    }

    return items.map((s) => DanceThreshold.fromValue(s));
  }

  public get isExclusive(): boolean {
    return this.startsWithAny([and, andX]) && this.data.indexOf(",", 4) !== -1;
  }

  public get description(): string {
    const prefix = this.isExclusive ? "all" : "any";
    const connector = this.isExclusive ? "and" : "or";
    const thresholds = this.danceThresholds;

    switch (thresholds.length) {
      case 0:
        return `songs`;
      case 1:
        return `${thresholds[0].description} songs`;
      case 2:
        return `songs danceable to ${prefix} of ${thresholds[0].description} ${connector} ${thresholds[1].description}`;
      default: {
        const last = thresholds.pop();
        return `songs danceable to ${prefix} of ${thresholds.map((t) => t.description).join(", ")} ${connector} ${last}`;
      }
    }
  }

  public get shortDescription(): string {
    return this.danceThresholds.map((t) => t.shortDescription).join(", ");
  }

  private startsWithAny(qualifiers: string[]): boolean {
    return !!qualifiers.find((q) => this.startsWith(q));
  }

  private startsWith(qualifier: string) {
    return this.data.toUpperCase().startsWith(qualifier + ",");
  }
}
