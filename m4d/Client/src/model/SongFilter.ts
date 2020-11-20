import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";
import { DanceQuery } from "./DanceQuery";
import { DanceQueryBase } from "./DanceQueryBase";
import { PurchaseInfo } from "./Purchase";
import { RawDanceQuery } from "./RawDanceQuery";
import { SongSort } from "./SongSort";
import { TagList } from "./TagList";
import { UserQuery } from "./UserQuery";

const subChar = "\u001a";
const scRegEx = new RegExp(subChar, "g");

@jsonObject
export class SongFilter {
  public static buildFilter(input: string): SongFilter {
    const filter = new SongFilter();

    const cells = SongFilter.splitFilter(input);

    filter.action = SongFilter.readCell(cells, 0);
    filter.dances = SongFilter.readCell(cells, 1);
    filter.sortOrder = SongFilter.readCell(cells, 2);
    filter.searchString = SongFilter.readCell(cells, 3);
    filter.purchase = SongFilter.readCell(cells, 4);
    filter.user = SongFilter.readCell(cells, 5);
    filter.tempoMin = SongFilter.readNumberCell(cells, 6);
    filter.tempoMax = SongFilter.readNumberCell(cells, 7);
    filter.page = SongFilter.readNumberCell(cells, 8);
    filter.tags = SongFilter.readCell(cells, 9);
    filter.level = SongFilter.readNumberCell(cells, 10);

    return filter;
  }

  private static splitFilter(input: string): string[] {
    return input
      .replace(/\\-/g, subChar)
      .split("-")
      .map((s) => s.trim().replace(scRegEx, "-"));
  }

  private static readCell(cells: string[], index: number): string | undefined {
    return cells.length >= index && cells[index] && cells[index] !== "."
      ? cells[index]
      : undefined;
  }

  private static readNumberCell(
    cells: string[],
    index: number
  ): number | undefined {
    const val =
      cells.length >= index && cells[index] && cells[index] !== "."
        ? cells[index]
        : undefined;

    return val ? Number.parseFloat(val) : undefined;
  }

  @jsonMember public action?: string = "index";
  @jsonMember public searchString?: string;
  @jsonMember public dances?: string;
  @jsonMember public sortOrder?: string;
  @jsonMember public purchase?: string;
  @jsonMember public user?: string;
  @jsonMember public tempoMin?: number;
  @jsonMember public tempoMax?: number;
  @jsonMember public page?: number;
  @jsonMember public tags?: string;
  @jsonMember public level?: number;

  public clone(): SongFilter {
    return SongFilter.buildFilter(this.query);
  }

  public get encodedQuery(): string {
    return encodeURIComponent(this.query);
  }

  public get query(): string {
    const tempoMin = this.tempoMin ? this.tempoMin.toString() : "";
    const tempoMax = this.tempoMax ? this.tempoMax.toString() : "";
    const level = this.level ? this.level : "";

    const ret =
      `${this.action}-${this.danceQuery.query}-${this.encode(
        this.sortOrder
      )}-` +
      `${this.encode(this.searchString)}-${this.encode(
        this.purchase
      )}-${this.encode(this.user)}-` +
      `${tempoMin}-${tempoMax}-${this.cleanPage}-${this.encode(
        this.tags
      )}-${level}`;

    return this.trimEnd(ret, ".-");
  }

  public get isRaw(): boolean {
    const action = this.action ?? "".toLowerCase().replace(" ", "+");
    return action.startsWith("azure+raw") || action === "holidaymusic";
  }

  public get danceQuery(): DanceQueryBase {
    return this.isRaw
      ? new RawDanceQuery(this.dances, this.tags)
      : new DanceQuery(this.dances);
  }

  public get userQuery(): UserQuery {
    return new UserQuery(this.user);
  }

  public get sort(): SongSort {
    return new SongSort(this.sortOrder);
  }

  public get tagList(): TagList {
    return new TagList(this.tags);
  }

  public get isEmpty(): boolean {
    return this.isEmptyExcept(["action", "sortOrder"]);
  }

  public get cleanPage(): string {
    return this.page && this.page > 1 ? this.page.toString() : "";
  }

  public get singleDance(): boolean {
    return this.danceQuery.singleDance;
  }

  public isDefault(user?: string): boolean {
    return (
      !this.isRaw &&
      this.isEmptyExcept(["action", "sortOrder", "user"]) &&
      this.isDefaultUser(user)
    );
  }

  public isDefaultDance(danceId: string, user?: string): boolean {
    return (
      !this.isRaw &&
      this.isEmptyExcept(["action", "dance", "sortOrder", "user"]) &&
      this.isDefaultUser(user) &&
      danceId?.toLowerCase() === this.dances?.toLowerCase()
    );
  }

  public isSimple(user?: string): boolean {
    return (
      !this.isRaw &&
      this.isEmptyExcept([
        "action",
        "user",
        "searchString",
        "sortOrder",
        "dances",
      ]) &&
      this.danceQuery.isSimple &&
      this.userQuery.isDefault(user)
    );
  }

  public get description(): string {
    // All [dance] songs [containing the text "<SearchString>] [Available on
    //  [Amazon|ITunes|Spotify] [Including tags TI] [Excluding tags TX] [between Tempo Range]
    //  [[not] (liked|disliked|edited) by user] sorted by [Sort Order] from [High|low] to [low|high]

    return this.isRaw
      ? this.purchase ?? "Raw Search"
      : `All${this.describePart(this.danceQuery.description)}` +
          `${this.describePart(this.describeKeywords)}` +
          `${this.describePart(this.describePurchase)}` +
          `${this.describePart(this.describeIncludedTags)}` +
          `${this.describePart(this.describeExcludedTags)}` +
          `${this.describePart(this.describeTempo)}` +
          `${this.describePart(this.userQuery.description)}` +
          `${this.describePart(this.describeSort)}.`;
  }

  public extractDefault(user?: string): SongFilter {
    const filter = new SongFilter();
    filter.action = this.action;
    filter.sortOrder = this.sortOrder;
    if (this.isDefaultUser(user)) {
      filter.user = this.user;
    }
    return filter;
  }

  public changeSort(order: string): SongFilter {
    const clone = this.clone();
    clone.sortOrder = this.sort.change(order).query;
    return clone;
  }

  public get url(): string {
    return `/song/${this.action ?? "index"}?filter=${this.encodedQuery}`;
  }

  private describePart(part: string | undefined): string {
    return part ? ` ${part}` : "";
  }

  private get describeKeywords(): string {
    if (!this.searchString) {
      return "";
    }

    return `containing the text "${this.searchString}"`;
  }

  private get describePurchase(): string {
    const services = PurchaseInfo.NamesFromFilter(this.purchase);
    if (!services.length) {
      return "";
    }

    return `available on ${services.join(" or ")}`;
  }

  private get describeIncludedTags(): string {
    return this.tagList.filterCategories(["Dances"]).AddsDescription;
  }

  private get describeExcludedTags(): string {
    return this.tagList.filterCategories(["Dances"]).RemovesDescription;
  }

  private get describeTempo(): string {
    if (this.tempoMin && this.tempoMax) {
      return `having tempo between ${this.tempoMin} and ${this.tempoMax} beats per minute`;
    } else if (this.tempoMin) {
      return `having tempo greater than ${this.tempoMin} beats per minute`;
    } else if (this.tempoMax) {
      return `having tempo less than ${this.tempoMax} beats per minute`;
    } else {
      return "";
    }
  }

  private get describeSort(): string {
    return this.sort.description;
  }

  private isDefaultUser(user?: string): boolean {
    return this.userQuery.isDefault(user);
  }

  private isEmptyExcept(properties: string[]): boolean {
    for (const key in this) {
      if (properties.includes(key)) {
        continue;
      }
      if (this[key]) {
        return false;
      }
    }
    return true;
  }

  private encode(s: string | undefined): string {
    const ret = s ? s.replace(/-/g, "\\-") : "";
    // // tslint:disable-next-line:no-console
    // console.log(ret);
    return ret;
  }

  private trimEnd(s: string, charlist: string): string {
    return s.replace(new RegExp("[" + charlist + "]+$"), "");
  }
}
