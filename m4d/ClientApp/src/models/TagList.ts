import { Tag } from "./Tag";

export class TagList {
  public static build(tags: Tag[]): TagList {
    return new TagList(tags.map((t) => t.toString()).join("|"));
  }

  public static concat(a: Tag[], b: Tag[]): Tag[] {
    const acc = new Map<string, number>(a.map((t) => [t.key, t.count ?? 0] as [string, number]));
    b.forEach((t) => {
      const key = t.key;
      acc.set(key, (t.count ?? 0) + (acc.get(key) ?? 0));
    });
    const ret: Tag[] = [];
    acc.forEach((v, k) => ret.push(Tag.fromKey(k, v)));
    ret.sort((a, b) => a.toString().localeCompare(b.toString()));
    return ret;
  }

  constructor(public summary?: string) {}

  public get tags(): Tag[] {
    if (!this.summary) {
      return [];
    }
    // Ignore ^ prefix for tag parsing (now: ^ means do NOT include dance_ALL tags)
    let s = this.summary;
    if (s.startsWith("^")) s = s.substring(1);
    return s.split("|").map((t) => Tag.fromString(t));
  }

  public get Adds(): Tag[] {
    return this.isQualified ? this.extract("+") : this.tags;
  }

  public get Removes(): Tag[] {
    return this.isQualified ? this.extract("-") : [];
  }

  public get AddsDescription(): string {
    return this.FormatList(this.Adds, "including tag", "and");
  }

  public get RemovesDescription(): string {
    return this.FormatList(this.Removes, "excluding tag", "or");
  }

  public get AddsShortDescription(): string {
    return this.FormatList(this.Adds, "inc", "and");
  }

  public get RemovesShortDescription(): string {
    return this.FormatList(this.Removes, "excl", "or");
  }

  public filterCategories(categories: string[]): TagList {
    const cats = categories.map((cat) => cat.toLowerCase());
    return TagList.build(this.tags.filter((t) => cats.indexOf(t.category.toLowerCase()) === -1));
  }

  public find(tag: Tag): Tag | undefined {
    const key = tag.key.toLowerCase();
    return this.tags.find((t) => t.key.toLowerCase() === key);
  }

  public remove(tag: Tag): TagList {
    const key = tag.key.toLowerCase();
    return TagList.build(this.tags.filter((t) => t.key.toLowerCase() !== key));
  }

  public add(tag: Tag): TagList {
    // Remove the tag first if it exists (to avoid duplicates), then add the new one
    const withoutTag = this.remove(tag);
    const newTags = [...withoutTag.tags, tag];
    return TagList.build(newTags);
  }

  public voteFromTags(tag: Tag): boolean | undefined {
    if (this.find(tag)) {
      return true;
    } else if (this.find(tag.negated)) {
      return false;
    } else {
      return undefined;
    }
  }

  private extract(prefix: string): Tag[] {
    return this.tags
      .filter((t) => t.value[0] === prefix)
      .map(
        (t) =>
          new Tag({
            key: t.key.substr(1),
            count: t.count,
          }),
      );
  }

  private get isQualified(): boolean {
    // Ignore ^ prefix for qualification check
    if (!this.summary) return false;
    const s = this.summary.startsWith("^") ? this.summary.substring(1) : this.summary;
    return s[0] === "+" || s[0] === "-";
  }

  private FormatList(list: Tag[], prefix: string, connector: string): string {
    const count = list.length;
    if (list.length === 0) {
      return "";
    }

    const tags = list.map((t) => t.value);

    if (count < 3) {
      const conn = ` ${connector} `;
      return `${prefix}${count > 1 ? "s" : ""} ${tags.join(conn)}`;
    } else {
      const last = tags.pop();
      return `${prefix}s ${tags.join(", ")} ${connector} ${last}`;
    }
  }
}
