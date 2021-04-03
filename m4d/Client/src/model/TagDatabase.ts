import { Tag } from "./Tag";
import { TagGroup } from "./TagGroup";
import { TagList } from "./TagList";

export class TagDatabase {
  constructor(
    private tagGroups?: TagGroup[],
    private incrementalTags?: TagGroup[]
  ) {}

  public get tags(): Tag[] {
    if (!this._tagCache && this.tagGroups) {
      this._tagCache = TagGroup.ToTags(this.allGroups);
    }
    return this._tagCache ?? [];
  }

  public addTag(key: string): void {
    const keyL = key.toLowerCase();
    const groups = this.groupMap!;
    const tag = groups.get(keyL);
    if (tag) {
      tag.count = (tag.count ?? 0) + 1;
    } else {
      const tagGroup = new TagGroup({
        key: key,
        modified: new Date(Date.now()),
        count: 1,
      });
      groups.set(keyL, tagGroup);
      this._tagCache!.push(Tag.fromString(key));
      if (this.incrementalTags) {
        this.incrementalTags.push(tagGroup);
      } else {
        this.incrementalTags = [tagGroup];
      }

      sessionStorage.setItem(
        "incremental-tags",
        JSON.stringify(this.incrementalTags)
      );
    }
  }

  public getPrimary(key: string): TagGroup | undefined {
    const group = this.getGroup(key);
    if (!group) {
      return;
    }
    return group.primaryId ? this.getPrimary(group.primaryId) : group;
  }

  public getGroup(key: string): TagGroup | undefined {
    return this.groupMap.get(key.toLowerCase());
  }

  public normalizeTagList(list: TagList, count?: number): TagList {
    return TagList.build(
      list.tags.map((t) => {
        const p = this.getPrimary(t.key);
        return new Tag({
          value: p?.value ?? t.value,
          category: p?.category ?? t.category,
          count: count ?? t.count,
        });
      })
    );
  }

  private get groupMap(): Map<string, TagGroup> {
    if (!this._groupMap && this.tagGroups) {
      this._groupMap = new Map<string, TagGroup>(
        this.allGroups.map((x) => [x.key.toLowerCase(), x])
      );
    }
    return this._groupMap ?? new Map<string, TagGroup>();
  }

  private get allGroups(): TagGroup[] {
    if (!this.tagGroups) {
      return [];
    }

    return this.incrementalTags
      ? [
          ...this.tagGroups.filter((tg) => !tg.primaryId),
          ...this.incrementalTags!,
        ]
      : this.tagGroups;
  }

  private _tagCache?: Tag[];
  private _groupMap?: Map<string, TagGroup>;
}
