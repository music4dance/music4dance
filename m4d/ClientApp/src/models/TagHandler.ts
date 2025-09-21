import { v4 as uuidv4 } from "uuid";
import { SongFilter } from "./SongFilter";
import { Tag, TagContext } from "./Tag";
import { TaggableObject } from "./TaggableObject";
import { DanceQuery } from "./DanceQuery";
import { DanceQueryItem } from "./DanceQueryItem";
import { TagQuery } from "./TagQuery";
import { TagList } from "./TagList";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";

export interface TagOption {
  type: "filter" | "list";
  scope: "dance-specific" | "global";
  modifier: "+" | "-";
  label: string;
  description: string;
  variant: "success" | "danger";
  href: string;
}

export class TagHandler {
  public id: string;
  public tag: Tag = new Tag();
  public user?: string;
  public filter?: SongFilter;
  public parent?: TaggableObject;
  public context?: TagContext | TagContext[]; // Context for which tag types to show
  public danceId?: string; // For dance-specific tag filtering

  constructor(init?: Partial<TagHandler>) {
    Object.assign(this, init);
    this.id = init?.tag?.key ?? uuidv4();
  }

  public get isSelected(): boolean {
    const parent = this.parent;
    return parent ? parent.isUserTag(this.tag) : false;
  }

  public get hasFilter(): boolean {
    return !!this.filter && !this.filter.isDefault(this.user) && !this.filter.isRaw;
  }

  public get computedDanceName(): string {
    // First try to get from danceId if it's a dance-specific context
    if (this.danceId) {
      return safeDanceDatabase().danceFromId(this.danceId)?.name ?? "";
    }

    // Then try to get from filter's danceQuery
    const danceNames = this.filter?.danceQuery.danceNames;
    if (danceNames && danceNames.length > 0) {
      return danceNames[0];
    }

    // Return empty string as fallback
    return "";
  }

  public get isDanceSpecific(): boolean {
    // Only show dance-specific options if we have a danceId context AND this is actually a dance tag
    // Don't show dance-specific options for regular song tags just because we're filtered to a single dance
    return !!this.danceId && !!this.computedDanceName;
  }

  public getSongFilter(modifier: string, danceSpecific: boolean, clear: boolean): SongFilter {
    const baseFilter = clear || !this.filter ? new SongFilter() : this.filter.clone();
    baseFilter.action = "filtersearch";

    if (danceSpecific && this.danceId) {
      // For dance-specific filtering, put the tag in the DanceQuery
      const singleTagString = modifier + this.tag.key;
      const tagList = new TagList(singleTagString);
      const tagQuery = TagQuery.fromParts(tagList, false);

      if (baseFilter.dances) {
        const existingDanceQuery = new DanceQuery(baseFilter.dances);
        const existingDanceItems = existingDanceQuery.danceQueryItems;
        const existingDanceItem = existingDanceItems.find((item) => item.id === this.danceId);
        const newDanceItem = new DanceQueryItem({
          id: this.danceId,
          threshold: existingDanceItem?.threshold ?? 1,
          tags: existingDanceItem?.tags
            ? existingDanceItem.tagQuery?.addTag(this.tag.key, modifier === "+")?.query
            : tagQuery.query,
        });
        const filteredDanceItems = existingDanceItems.filter((item) => item.id !== this.danceId);
        const allDanceItems = [...filteredDanceItems, newDanceItem];
        const allDanceStrings = allDanceItems.map((item) => item.toString());
        const combinedDanceQuery = DanceQuery.fromParts(allDanceStrings, true);
        baseFilter.dances = combinedDanceQuery.query;
      } else {
        const newDanceItem = new DanceQueryItem({
          id: this.danceId,
          threshold: 1,
          tags: tagQuery.query,
        });
        const danceQuery = new DanceQuery(newDanceItem.toString());
        baseFilter.dances = danceQuery.query;
      }
    } else {
      // For global tags, create TagList and use fromParts
      const singleTagString = modifier + this.tag.key;
      const tagList = new TagList(singleTagString);

      if (baseFilter.tags) {
        // Parse existing tag query and add the new tag to it
        const existingTagQuery = new TagQuery(baseFilter.tags);
        const updatedTagQuery = existingTagQuery.addTag(this.tag.key, modifier === "+");
        baseFilter.tags = updatedTagQuery.query;
      } else {
        baseFilter.tags = TagQuery.fromParts(tagList).query;
      }
    }

    return baseFilter;
  }

  public getTagLink(modifier: string, danceSpecific: boolean, clear: boolean): string {
    const filter = this.getSongFilter(modifier, danceSpecific, clear);
    return `/song/filtersearch?filter=${filter.encodedQuery}`;
  }

  public getFilterDescription(modifier: string, danceSpecific: boolean, clear: boolean): string {
    const filter = this.getSongFilter(modifier, danceSpecific, clear);
    return filter.description;
  }

  public getAvailableOptions(): TagOption[] {
    const options: TagOption[] = [];
    const danceName = this.computedDanceName;

    // Dance-specific options (only show if we have a specific dance and valid dance name)
    if (this.isDanceSpecific) {
      // Filter options (only show if there's an existing filter)
      if (this.hasFilter) {
        options.push({
          type: "filter",
          scope: "dance-specific",
          modifier: "+",
          label: `Filter current list to ${danceName} songs tagged as ${this.tag.value}`,
          description: this.getFilterDescription("+", true, false),
          variant: "success",
          href: this.getTagLink("+", true, false),
        });

        options.push({
          type: "filter",
          scope: "dance-specific",
          modifier: "-",
          label: `Filter current list to ${danceName} songs not tagged as ${this.tag.value}`,
          description: this.getFilterDescription("-", true, false),
          variant: "danger",
          href: this.getTagLink("-", true, false),
        });
      }

      // List all options (always show for dance-specific)
      options.push({
        type: "list",
        scope: "dance-specific",
        modifier: "+",
        label: `List all ${danceName} songs tagged as ${this.tag.value}`,
        description: this.getFilterDescription("+", true, true),
        variant: "success",
        href: this.getTagLink("+", true, true),
      });

      options.push({
        type: "list",
        scope: "dance-specific",
        modifier: "-",
        label: `List all ${danceName} songs not tagged as ${this.tag.value}`,
        description: this.getFilterDescription("-", true, true),
        variant: "danger",
        href: this.getTagLink("-", true, true),
      });
    }

    // Global option
    // Filter options (only show if there's an existing filter and not dance-specific)
    if (this.hasFilter && !this.isDanceSpecific) {
      options.push({
        type: "filter",
        scope: "global",
        modifier: "+",
        label: `Filter current list to songs tagged as ${this.tag.value}`,
        description: this.getFilterDescription("+", false, false),
        variant: "success",
        href: this.getTagLink("+", false, false),
      });

      options.push({
        type: "filter",
        scope: "global",
        modifier: "-",
        label: `Filter current list to songs not tagged as ${this.tag.value}`,
        description: this.getFilterDescription("-", false, false),
        variant: "danger",
        href: this.getTagLink("-", false, false),
      });
    }

    // List all options (always show for global)
    options.push({
      type: "list",
      scope: "global",
      modifier: "+",
      label: `List all songs tagged as ${this.tag.value}`,
      description: this.getFilterDescription("+", false, true),
      variant: "success",
      href: this.getTagLink("+", false, true),
    });

    options.push({
      type: "list",
      scope: "global",
      modifier: "-",
      label: `List all songs not tagged as ${this.tag.value}`,
      description: this.getFilterDescription("-", false, true),
      variant: "danger",
      href: this.getTagLink("-", false, true),
    });

    return options;
  }
}
