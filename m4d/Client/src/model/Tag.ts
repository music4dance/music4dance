// tslint:disable: max-classes-per-file
import { jsonMember, jsonObject, TypedJSON } from "typedjson";

export interface TagInfo {
  iconName: string;
  description: string;
}

@jsonObject
export class Tag {
  public get key(): string {
    return `${this.value}:${this.category}`;
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

  public static get TagInfo() {
    return this.tagInfo;
  }

  public static fromString(key: string): Tag {
    const parts = key.split(":");
    const count = parts.length > 1 ? Number.parseInt(parts[2], 10) : undefined;

    return new Tag({ value: parts[0], category: parts[1], count });
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

  @jsonMember public value!: string;
  @jsonMember public category!: string;
  @jsonMember public count?: number;
  @jsonMember public primaryId?: string;

  public constructor(init?: Partial<Tag>) {
    Object.assign(this, init);
  }

  public get negated(): Tag {
    const value = this.value.startsWith("!")
      ? this.value.substring(1)
      : "!" + this.value;
    return new Tag({ value, category: this.category, count: this.count });
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
        value: t.value,
        category: t.category,
        count: t.count,
        bucket: Math.floor(idx / bucketSize),
      })!;
    });
  }

  @jsonMember public bucket?: number;
}
