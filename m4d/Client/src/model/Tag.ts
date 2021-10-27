import "reflect-metadata";
import { jsonMember, jsonObject, TypedJSON } from "typedjson";
import { DanceEnvironment } from "./DanceEnvironmet";

declare const environment: DanceEnvironment;

export enum TagCategory {
  Style = "Style",
  Tempo = "Tempo",
  Music = "Music",
  Other = "Other",
  Dance = "Dance",
}

export interface TagInfo {
  iconName: string;
  description: string;
}

@jsonObject
export class Tag {
  public get value(): string {
    if (!this.key) {
      throw new Error("Invalid Tag");
    }
    const parts = this.key.split(":");
    return parts[0];
  }

  public get category(): string {
    if (!this.key) {
      throw new Error("Invalid Tag");
    }
    const parts = this.key.split(":");
    return parts[1];
  }

  public get variant(): string | undefined {
    const cat = this.category.toLocaleLowerCase();
    if (Tag.tagInfo.has(cat)) {
      return cat;
    }
    return undefined;
  }

  public get icon(): string | undefined {
    const variant = this.variant;
    return variant ? Tag.tagInfo.get(variant)?.iconName : undefined;
  }

  public toString(): string {
    const key = this.key;
    const count = this.count;
    return count ? key + `:${count}` : key;
  }

  public static get TagInfo(): Map<string, TagInfo> {
    return this.tagInfo;
  }

  public static fromString(key: string): Tag {
    const parts = key.split(":");
    const count = parts.length > 2 ? Number.parseInt(parts[2], 10) : undefined;

    return Tag.fromParts(parts[0], parts[1], count);
  }

  public static fromDanceId(id: string): Tag {
    return Tag.fromParts(environment.fromId(id)!.name, TagCategory.Dance);
  }

  public static fromKey(key: string, count?: number): Tag {
    return new Tag({ key: key, count: count ?? 0 });
  }

  public static fromParts(
    value: string,
    category: string,
    count?: number
  ): Tag {
    return new Tag({ key: `${value}:${category}`, count: count ?? 0 });
  }

  public static get tagKeys(): string[] {
    return [...Tag.tagInfo.keys()];
  }

  private static tagInfo = new Map<string, TagInfo>([
    ["style", { iconName: "briefcase", description: "style" }],
    ["tempo", { iconName: "clock", description: "tempo" }],
    ["music", { iconName: "music-note-list", description: "musical genre" }],
    ["other", { iconName: "tag", description: "other" }],
    ["dance", { iconName: "award", description: "dance" }],
  ]);

  @jsonMember public key!: string;
  @jsonMember public count?: number;

  public constructor(init?: Partial<Tag>) {
    Object.assign(this, init);
  }

  public get positive(): boolean {
    return !this.value.startsWith("!");
  }

  public get negated(): Tag {
    const value = this.value;
    return Tag.fromParts(
      this.positive ? "!" + value : value.substring(1),
      this.category,
      this.count
    );
  }

  public get neutral(): Tag {
    const key = this.key;
    return new Tag({
      key: this.positive ? key : key.substring(1),
      count: this.count,
    });
  }
}

@jsonObject
export class TagBucket extends Tag {
  public static bucketize(tags: Tag[], bucketCount = 10): TagBucket[] {
    const ordered = tags.sort((a, b) => (a.count ?? 0) - (b.count ?? 0));

    const bucketSize = ordered.length / bucketCount;

    const serializer = new TypedJSON(TagBucket);
    return ordered.map((t, idx) => {
      return serializer.parse({
        key: t.key,
        count: t.count,
        bucket: Math.floor(idx / bucketSize),
      })!;
    });
  }

  @jsonMember public bucket?: number;
}
