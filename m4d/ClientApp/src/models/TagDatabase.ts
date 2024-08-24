import { Tag } from "./Tag";

export class TagDatabase {
  constructor(
    private tagList?: Tag[],
    private incrementalTags?: Tag[],
  ) {}

  public addTag(key: string): void {
    const keyL = key.toLowerCase();
    const groups = this.map!;
    const tag = groups.get(keyL);
    if (tag) {
      tag.count = (tag.count ?? 0) + 1;
    } else {
      const tag = new Tag({
        key: key,
        count: 1,
      });
      groups.set(keyL, tag);
      if (this.incrementalTags) {
        this.incrementalTags.push(tag);
      } else {
        this.incrementalTags = [tag];
      }

      sessionStorage.setItem("incremental-tags", JSON.stringify(this.incrementalTags));
    }
  }

  public get tags(): Tag[] {
    if (!this.tagList) {
      return [];
    }

    return this.incrementalTags ? [...this.tagList, ...this.incrementalTags!] : this.tagList;
  }

  public getTag(key: string): Tag | undefined {
    return this.map.get(key.toLowerCase());
  }

  private get map(): Map<string, Tag> {
    if (!this._map && this.tagList) {
      this._map = new Map<string, Tag>(this.tags.map((x) => [x.key.toLowerCase(), x]));
    }
    return this._map ?? new Map<string, Tag>();
  }

  private _map?: Map<string, Tag>;
}
