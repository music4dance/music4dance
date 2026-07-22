import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { DanceQueryBase } from "./DanceQueryBase";
import { DanceQueryItem } from "./DanceQueryItem";

const all = "ALL";
const and = "AND"; // Exclusive + Explicit
const andX = "ADX"; // Exclusive + Inferred
const oneOfX = "OOX"; // Inclusive + Inferred

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
    this.data = DanceQuery.normalizeQuery(query ? query : "");
    if (all === this.data.toUpperCase()) {
      this.data = "";
    }
  }

  // Normalize the query to map inferred operators to explicit ones
  private static normalizeQuery(query: string): string {
    if (query.toUpperCase().startsWith(andX + ",")) {
      return and + "," + query.substring(andX.length + 1);
    }
    if (query.toUpperCase().startsWith(oneOfX + ",")) {
      return query.substring(oneOfX.length + 1); // Remove OOX, treat as inclusive (no prefix)
    }
    return query;
  }

  public get query(): string {
    return this.data;
  }

  public get danceQueryItems(): DanceQueryItem[] {
    const items = this.data
      .split(",")
      .map((s) => s.trim())
      .filter((s) => s);

    // Remove only AND prefix if present
    if (items.length > 0 && items[0]?.toUpperCase() === and) {
      items.shift();
    }

    return items.map((s) => DanceQueryItem.fromValue(s));
  }

  public get isExclusive(): boolean {
    // Exclusive if starts with AND and more than one dance
    return this.startsWith(and) && this.data.indexOf(",", and.length + 1) !== -1;
  }

  // A marked plain dance is its own target; a marked DanceGroup has no per-dance
  // rating/tempo fields of its own, so it only resolves when the item's primaryTargetId
  // names one of the group's members (this is how selecting a group's member dance in
  // the scope chooser is represented, even though that member was never itself a
  // top-level selected item). Mirrors DanceQuery.PrimaryDanceId (m4dModels/DanceQuery.cs).
  public override get primaryDanceId(): string | undefined {
    for (const item of this.danceQueryItems) {
      if (!item.primary) {
        continue;
      }
      // Lazy: only touch the database once an actual marked item is found, so filters with
      // no marker never require a dance database to be loaded (e.g. in tests that construct
      // a SongFilter without one).
      const dance = safeDanceDatabase().fromId(item.id);
      if (dance && DanceGroup.isGroup(dance)) {
        const targetId = item.primaryTargetId;
        const member = targetId
          ? dance.dances.find((d) => d.id.toUpperCase() === targetId.toUpperCase())
          : undefined;
        if (member) {
          // Return the canonical member id, not the raw (possibly differently cased) target
          // from the filter string - downstream OData field paths like dance_{id}/Votes must
          // match the indexed field name exactly. Mirrors DanceQuery.PrimaryDanceId.
          return member.id;
        }
        continue;
      }
      return item.id;
    }
    return undefined;
  }

  public get description(): string {
    const prefix = this.isExclusive ? "all" : "any";
    const connector = this.isExclusive ? "and" : "or";
    const items = this.danceQueryItems.slice();

    switch (items.length) {
      case 0:
        return `songs`;
      case 1:
        return `${items[0]?.description} songs`;
      case 2:
        return `songs danceable to ${prefix} of ${items[0]?.description} ${connector} ${items[1]?.description}`;
      default: {
        const last = items.pop();
        return `songs danceable to ${prefix} of ${items.map((t) => t.description).join(", ")} ${connector} ${last?.description}`;
      }
    }
  }

  public get shortDescription(): string {
    return this.danceQueryItems.map((t) => t.shortDescription).join(", ");
  }

  private startsWith(qualifier: string) {
    return this.data.toUpperCase().startsWith(qualifier + ",");
  }
}
