import { toTitleCase } from "@/helpers/StringHelpers";

export enum SortOrder {
  Title = "Title",
  Artist = "Artist",
  Dances = "Dances",
  Tempo = "Tempo",
  Length = "Length",
  Modified = "Modified",
  Created = "Created",
  Edited = "Edited",
  Energy = "Energy",
  Mood = "Mood",
  Beat = "Beat",
  Comments = "Comments",
  Match = "Closest",
  Default = "",
}

export class SongSort {
  public static fromParts(order?: string, direction?: string, hasQuery?: boolean): SongSort {
    const dir = direction?.toLowerCase() === "desc" ? "desc" : undefined;
    return order
      ? direction
        ? new SongSort(order + "_" + dir, hasQuery)
        : new SongSort(order, hasQuery)
      : new SongSort(undefined, hasQuery);
  }

  private data: string;
  private hasQuery: boolean;

  public constructor(query?: string, hasQuery?: boolean) {
    this.data = this.normalize(query);
    this.hasQuery = !!hasQuery;
  }

  public get query(): string {
    return this.data;
  }

  public get id(): string | undefined {
    return this.data ? this.data.split("_")[0] : undefined;
  }

  private get computedId(): string {
    return this.data
      ? (this.data.split("_")[0] ?? "")
      : this.hasQuery
        ? SortOrder.Match
        : SortOrder.Dances;
  }

  public get direction(): string {
    return this.data && this.data.endsWith("_desc") ? "desc" : "asc";
  }

  public get friendlyName(): string {
    switch (this.computedId) {
      case SortOrder.Dances:
        return "Dance Rating";
      case SortOrder.Modified:
        return "Last Modified";
      case SortOrder.Edited:
        return "Last Edited";
      case SortOrder.Created:
        return "When Added";
      case SortOrder.Match:
        return "Closest Match";
      default:
        return this.computedId;
    }
  }

  public get type(): string {
    switch (this.computedId) {
      case SortOrder.Tempo:
      case SortOrder.Beat:
      case SortOrder.Mood:
      case SortOrder.Energy:
        return "numeric";
      case SortOrder.Modified:
      case SortOrder.Created:
      case SortOrder.Edited:
      case SortOrder.Dances:
        return "";
      default:
        return "alpha";
    }
  }

  public get isChronological(): boolean {
    switch (this.computedId) {
      case SortOrder.Modified:
      case SortOrder.Created:
      case SortOrder.Edited:
      case SortOrder.Comments:
        return true;
      default:
        return false;
    }
  }

  public get description(): string {
    const prefix = `sorted by ${this.friendlyName}`;
    return this.computedId === SortOrder.Match
      ? prefix
      : prefix + ` from ${this.directionDescription}`;
  }

  private get directionDescription(): string {
    if (this.direction === "asc") {
      switch (this.computedId) {
        case SortOrder.Tempo:
          return "slowest to fastest";
        case SortOrder.Length:
          return "shortest to longest";
        case SortOrder.Modified:
        case SortOrder.Edited:
        case SortOrder.Created:
        case SortOrder.Comments:
          return "newest to oldest";
        case SortOrder.Dances:
          return "most popular to least popular";
        case SortOrder.Beat:
          return "weakest to strongest";
        case SortOrder.Mood:
          return "saddest to happiest";
        case SortOrder.Energy:
          return "lowest to highest";
        default:
          return "A to Z";
      }
    } else {
      switch (this.computedId) {
        case SortOrder.Tempo:
          return "fastest to slowest";
        case SortOrder.Length:
          return "longest to shortest";
        case SortOrder.Modified:
        case SortOrder.Edited:
        case SortOrder.Created:
        case SortOrder.Comments:
          return "oldest to newest";
        case SortOrder.Dances:
          return "least popular to most popular";
        case SortOrder.Beat:
          return "strongest to weakest";
        case SortOrder.Mood:
          return "happiest to saddest";
        case SortOrder.Energy:
          return "highest to lowest";
        default:
          return "Z to A";
      }
    }
  }

  private normalize(query?: string): string {
    if (!query) {
      return "";
    }

    const parts = query.split("_").map((p) => p.trim());
    return (
      toTitleCase(parts[0] || "") +
      (parts.length === 2 && parts[1]?.toLowerCase() === "desc" ? "_desc" : "")
    );
  }
}
