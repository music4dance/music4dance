import { jsonMember, jsonObject } from "typedjson";
import { DanceQuery } from "./DanceQuery";
import { DanceQueryBase } from "./DanceQueryBase";
import { PurchaseInfo } from "./Purchase";
import { RawDanceQuery } from "./RawDanceQuery";
import { SongSort, SortOrder } from "./SongSort";
import { UserQuery } from "./UserQuery";
import { KeywordQuery } from "./KeywordQuery";
import { TagQuery } from "./TagQuery";

const subChar = "\u001a";

@jsonObject
export class SongFilter {
  public static buildFilter(input: string): SongFilter {
    const filter = new SongFilter();

    const cells = SongFilter.splitFilter(input);

    let idx = 0;

    const versionString = SongFilter.readCell(cells, 0);

    filter.version = versionString?.toLocaleLowerCase() === "v2" ? 2 : 1;
    if (filter.version > 1) {
      idx += 1;
    }
    filter.action = SongFilter.readCell(cells, idx++);
    filter.dances = SongFilter.readCell(cells, idx++);
    filter.sortOrder = SongFilter.readCell(cells, idx++);
    filter.searchString = SongFilter.readCell(cells, idx++);
    filter.purchase = SongFilter.readCell(cells, idx++);
    filter.user = SongFilter.readCell(cells, idx++);
    filter.tempoMin = SongFilter.readNumberCell(cells, idx++);
    filter.tempoMax = SongFilter.readNumberCell(cells, idx++);
    if (filter.version > 1) {
      filter.lengthMin = SongFilter.readNumberCell(cells, idx++);
      filter.lengthMax = SongFilter.readNumberCell(cells, idx++);
    }
    filter.page = SongFilter.readNumberCell(cells, idx++);
    filter.tags = SongFilter.readCell(cells, idx++);
    filter.level = SongFilter.readNumberCell(cells, idx++);

    return filter;
  }

  private static splitFilter(input: string): string[] {
    return input
      .replaceAll("\\-", subChar)
      .split("-")
      .map((s) => s.trim().replaceAll(subChar, "-"));
  }

  private static readCell(cells: string[], index: number): string | undefined {
    return cells.length >= index && cells[index] && cells[index] !== "." ? cells[index] : undefined;
  }

  private static readNumberCell(cells: string[], index: number): number | undefined {
    const val =
      cells.length >= index && cells[index] && cells[index] !== "." ? cells[index] : undefined;

    return val ? Number.parseFloat(val) : undefined;
  }

  @jsonMember(Number) public version?: number = 1;
  @jsonMember(String) public action?: string = "index";
  @jsonMember(String) public searchString?: string;
  @jsonMember(String) public dances?: string;
  @jsonMember(String) public sortOrder?: string;
  @jsonMember(String) public purchase?: string;
  @jsonMember(String) public user?: string;
  @jsonMember(Number) public tempoMin?: number;
  @jsonMember(Number) public tempoMax?: number;
  @jsonMember(Number) public lengthMin?: number;
  @jsonMember(Number) public lengthMax?: number;
  @jsonMember(Number) public page?: number;
  @jsonMember(String) public tags?: string;
  @jsonMember(Number) public level?: number;

  public clone(): SongFilter {
    return SongFilter.buildFilter(this.query);
  }

  public get encodedQuery(): string {
    return encodeURIComponent(this.query);
  }

  public get query(): string {
    return this.isRaw ? this.rawFilterQuery : this.normalFilterQuery;
  }

  public get TextSearch(): boolean {
    return !!this.searchString;
  }

  private get normalFilterQuery(): string {
    const tempoMin = this.tempoMin ? this.tempoMin.toString() : "";
    const tempoMax = this.tempoMax ? this.tempoMax.toString() : "";
    const lengthMin = this.lengthMin ? this.lengthMin.toString() : "";
    const lengthMax = this.lengthMax ? this.lengthMax.toString() : "";
    const level = this.level ? this.level : "";

    const ret =
      `v2-${this.action}-${this.encode(this.danceQuery.query)}-${this.encode(this.sortOrder)}-` +
      `${this.encode(this.searchString)}-${this.encode(this.purchase)}-${this.encode(this.user)}-` +
      `${tempoMin}-${tempoMax}-${lengthMin}-${lengthMax}-${this.cleanPage}-${this.encode(
        this.tags,
      )}-${level}`;

    return this.trimEnd(ret, ".-");
  }

  private get rawFilterQuery(): string {
    const ret =
      `${this.action}-${this.encode(this.dances ?? "")}-${this.encode(this.sortOrder ?? "")}-` +
      `${this.encode(this.searchString)}-${this.encode(this.purchase ?? "")}-${this.encode(
        this.user ?? "",
      )}-` +
      `--${this.cleanPage}-${this.encode(this.tags)}-${this.level?.toString() ?? ""}`;

    return this.trimEnd(ret, ".-");
  }

  public getPlayListRef(user?: string): string | undefined {
    return this.getRef("createspotify", user);
  }

  public getExportRef(user?: string): string | undefined {
    return this.getRef("exportplaylist", user);
  }

  private getRef(type: string, user?: string): string | undefined {
    return !this.isDefaultDance(undefined, user)
      ? `/song/${type}?filter=${this.encodedQuery}`
      : undefined;
  }

  public get isRaw(): boolean {
    const action = this.action ?? "".toLowerCase().replace(" ", "+");
    return action.startsWith("azure+raw") || action === "customsearch";
  }

  public get keywordQuery(): KeywordQuery {
    return new KeywordQuery(this.searchString);
  }

  public get danceQuery(): DanceQueryBase {
    return this.isRaw ? new RawDanceQuery(this.dances, this.tags) : new DanceQuery(this.dances);
  }

  public get userQuery(): UserQuery {
    return new UserQuery(this.user);
  }

  public get sort(): SongSort {
    return new SongSort(this.sortOrder, this.TextSearch);
  }

  public get tagQuery(): TagQuery {
    return new TagQuery(this.tags);
  }

  public get isEmpty(): boolean {
    return this.isEmptyExcept(["action", "sortOrder", "version"]);
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
      this.isEmptyExcept(["action", "sortOrder", "user", "version"]) &&
      this.isDefaultUser(user)
    );
  }

  public isDefaultDance(danceId?: string, user?: string): boolean {
    return (
      !this.isRaw &&
      this.isEmptyExcept(["action", "dance", "sortOrder", "user", "version"]) &&
      this.isDefaultUser(user) &&
      (!danceId || danceId?.toLowerCase() === this.dances?.toLowerCase())
    );
  }

  public isSimple(user?: string): boolean {
    return (
      !this.isRaw &&
      this.isEmptyExcept([
        "version",
        "action",
        "user",
        "searchString",
        "sortOrder",
        "dances",
        "page",
      ]) &&
      !this.keywordQuery.isLucene &&
      this.danceQuery.isSimple &&
      this.userQuery.isDefault(user) &&
      !this.sortOrder?.startsWith(SortOrder.Comments)
    );
  }

  public get description(): string {
    // All [dance] songs [containing the text "<SearchString>] [Available on
    //  [Amazon|ITunes|Spotify] [Including tags TI] [Excluding tags TX] [between Tempo Range]
    //  [[not] (liked|disliked|edited) by user] sorted by [Sort Order] from [High|low] to [low|high]

    return this.isRaw
      ? (this.purchase ?? "Raw Search")
      : `All${this.describePart(this.danceQuery.description)}` +
          `${this.describePart(this.describeKeywords)}` +
          `${this.describePart(this.describePurchase)}` +
          `${this.describePart(this.tagQuery.description)}` +
          `${this.describePart(this.describeTempo)}` +
          `${this.describePart(this.describeLength)}` +
          `${this.describePart(this.userQuery.description)}` +
          `${this.describePart(this.describeComments)}` +
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
    const newSort = new SongSort(order, this.TextSearch);
    clone.sortOrder = newSort.query;
    return clone;
  }

  public get url(): string {
    return `/song/${this.action ?? "index"}?filter=${this.encodedQuery}`;
  }

  private describePart(part: string | undefined): string {
    return part ? ` ${part}` : "";
  }

  private get describeKeywords(): string {
    return this.keywordQuery.description;
  }

  private get describePurchase(): string {
    const services = PurchaseInfo.NamesFromFilter(this.purchase);
    if (!services.length) {
      return "";
    }

    return `available on ${services.join(" or ")}`;
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

  private get describeLength(): string {
    if (this.lengthMin && this.lengthMax) {
      return `having length between ${this.lengthMin} and ${this.lengthMax} seconds`;
    } else if (this.lengthMin) {
      return `having length greater than ${this.lengthMin} seconds`;
    } else if (this.lengthMax) {
      return `having length less than ${this.lengthMax} seconds`;
    } else {
      return "";
    }
  }

  private get describeComments(): string {
    return this.sortOrder?.startsWith(SortOrder.Comments)
      ? "including only songs with comments"
      : "";
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

  // TODONEXT:
  //  ✅ Make the tag modals handle dance tags
  //  ✅ Restore the add to filter options
  //  ✅ Fixed dance duplication in queries
  //  Make the tag clouds on dance pages handle dance tags
  //  Marginally Related: Add dance tags to tag list in dances page...
  private encode(s: string | undefined): string {
    return s ? s.replace(/-/g, subChar) : "";
  }

  private trimEnd(s: string, charlist: string): string {
    return s.replace(new RegExp("[" + charlist + "]+$"), "");
  }
}
