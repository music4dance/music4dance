import { toTitleCase } from "@/helpers/StringHelpers";

export enum SortOrder {
  Title = "Title",
  Artist = "Artist",
  Dances = "Dances",
  Tempo = "Tempo",
  Modified = "Modified",
  Created = "Created",
  Edited = "Edited",
  Energy = "Energy",
  Mood = "Mood",
  Beat = "Beat",
  Match = "",
}

export class SongSort {
  public static fromParts(
    order: string | undefined,
    direction: string | undefined
  ): SongSort {
    const dir = direction?.toLowerCase() === "desc" ? "desc" : undefined;
    return order
      ? direction
        ? new SongSort(order + "_" + dir)
        : new SongSort(order)
      : new SongSort();
  }

  private data: string;

  public constructor(query?: string) {
    this.data = this.normalize(query);
  }

  public get query(): string {
    return this.data;
  }

  public get order(): string | undefined {
    return this.data ? this.data.split("_")[0] : undefined;
  }

  public get direction(): string {
    return this.data && this.data.endsWith("_desc") ? "desc" : "asc";
  }

  public get friendlyName(): string {
    switch (this.order) {
      case SortOrder.Dances:
        return "Dance Rating";
      case SortOrder.Modified:
        return "Last Modified";
      case SortOrder.Edited:
        return "Last Edited";
      case SortOrder.Created:
        return "When Added";
      case undefined:
      case "":
        return "Closest Match";
      default:
        return this.order;
    }
  }

  public get type(): string {
    switch (this.order) {
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

  public get description(): string {
    return this.order
      ? `sorted by ${this.friendlyName} from ${this.directionDescription}`
      : "";
  }

  private get directionDescription(): string {
    if (this.direction === "asc") {
      switch (this.order) {
        case SortOrder.Tempo:
          return "slowest to fastest";
        case SortOrder.Modified:
        case SortOrder.Edited:
        case SortOrder.Created:
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
      switch (this.order) {
        case SortOrder.Tempo:
          return "fastest to slowest";
        case SortOrder.Modified:
        case SortOrder.Edited:
        case SortOrder.Created:
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

  public change(order: string): SongSort {
    const sorder = toTitleCase(order);
    if (sorder === this.order) {
      const direction = this.direction === "desc" ? undefined : "desc";
      return SongSort.fromParts(this.order, direction);
    } else {
      return new SongSort(order);
    }
  }

  private normalize(query: string | undefined): string {
    if (!query) {
      return "";
    }

    const parts = query.split("_").map((p) => p.trim());
    if (parts.length === 2 && parts[1].toLowerCase() === "desc") {
      return toTitleCase(parts[0]) + "_desc";
    }

    return parts[0];
  }
}
