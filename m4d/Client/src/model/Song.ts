import { DanceRatingDelta } from "@/DanceRatingDelta";
import { enumKeys } from "@/helpers/enumKeys";
import { pascalToCamel } from "@/helpers/StringHelpers";
import { timeOrder, timeOrderVerbose } from "@/helpers/timeHelpers";
import "reflect-metadata";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { AlbumDetails } from "./AlbumDetails";
import { DanceEnvironment } from "./DanceEnvironmet";
import { DanceRating } from "./DanceRating";
import { ModifiedRecord } from "./ModifiedRecord";
import { PurchaseInfo, ServiceType } from "./Purchase";
import { SongHistory } from "./SongHistory";
import { PropertyType, SongProperty } from "./SongProperty";
import { Tag } from "./Tag";
import { TaggableObject } from "./TaggableObject";
import { TagList } from "./TagList";

declare const environment: DanceEnvironment;

@jsonObject
export class Song extends TaggableObject {
  public static fromHistory(history: SongHistory, currentUser?: string): Song {
    const song = new Song();
    song.songId = history.id;
    song.loadProperties(history.properties, currentUser);
    return song;
  }

  @jsonMember public songId!: string;
  @jsonMember public title!: string;
  @jsonMember public artist!: string;
  @jsonMember public tempo?: number;
  @jsonMember public length?: number;
  @jsonMember public sample?: string;
  @jsonMember public danceability?: number;
  @jsonMember public energy?: number;
  @jsonMember public valence?: number;
  @jsonMember public created!: Date;
  @jsonMember public modified!: Date;
  @jsonMember public edited?: Date;
  @jsonArrayMember(DanceRating) public danceRatings?: DanceRating[];
  @jsonArrayMember(ModifiedRecord) public modifiedBy?: ModifiedRecord[];
  @jsonArrayMember(AlbumDetails) public albums?: AlbumDetails[];

  public constructor(init?: Partial<Song>) {
    super();
    Object.assign(this, init);
  }

  public compareToHistory(history: SongHistory, user?: string): boolean {
    const other = Song.fromHistory(history, user);

    const diffs: string[] = [];
    [
      "songId",
      "title",
      "artist",
      "tempo",
      "length",
      "sample",
      "danceability",
      "energy",
      "valence",
      // "created",
      // "modified",
    ].forEach((v) => this.validateField(v, other, diffs));

    this.tags.sort((a, b) => a.key.localeCompare(b.key));
    this.validateArray("tags", other, diffs);
    this.currentUserTags?.sort((a, b) => a.key.localeCompare(b.key));
    this.validateArray("currentUserTags", other, diffs);
    this.danceRatings?.sort((a, b) => a.id.localeCompare(b.id));
    this.danceRatings?.forEach((dr) => {
      dr.tags?.sort((a, b) => a.key.localeCompare(b.key));
      dr.currentUserTags?.sort((a, b) => a.key.localeCompare(b.key));
    });
    other.danceRatings?.sort((a, b) => a.id.localeCompare(b.id));
    this.validateArray("danceRatings", other, diffs);
    this.validateArray("modifiedBy", other, diffs);
    this.validateArray("albums", other, diffs);

    if (diffs.length > 0) {
      console.log(`Failed to validate song ${this.songId}`);
      diffs.forEach((s) => console.log(s));

      return false;
    }

    return true;
  }

  private validateField(name: string, other: Song, diffs: string[]): void {
    /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
    const tobj = this as any;
    /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
    const oobj = other as any;
    if (tobj[name] != oobj[name]) {
      diffs.push(`${name}\t${tobj[name]}\t${oobj[name]}`);
    }
  }

  private validateArray(name: string, other: Song, diffs: string[]): void {
    /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
    const tobj = this as any;
    /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
    const oobj = other as any;

    const thisJson = this.JSONstringifyOrder(tobj[name])?.toLowerCase();
    const otherJson = this.JSONstringifyOrder(oobj[name])?.toLowerCase();

    if (!thisJson || !otherJson) {
      return;
    }
    if (thisJson != otherJson) {
      diffs.push(`${name}\t${thisJson}\t${otherJson}`);
    }
  }

  JSONstringifyOrder(obj: unknown): string {
    const allKeys: (string | number)[] | null | undefined = [];
    JSON.stringify(obj, function (key, value) {
      allKeys.push(key);
      return value;
    });
    allKeys.sort();
    return JSON.stringify(obj, allKeys);
  }

  public getPurchaseInfo(service: ServiceType): PurchaseInfo | undefined {
    const album = this.albums?.find((a) => a.purchase?.decodeService(service));
    if (album) {
      return album.purchase?.decodeService(service);
    }
    return undefined;
  }

  public getPurchaseInfos(): PurchaseInfo[] {
    const ret = [];

    for (const service of enumKeys(ServiceType)) {
      const purchase = this.getPurchaseInfo(ServiceType[service]);
      if (purchase) {
        ret.push(purchase);
      }
    }

    return ret;
  }

  public findDanceRatingById(id: string): DanceRating | undefined {
    return this.danceRatings?.find((r) => r.id === id);
  }

  public findDanceRatingByName(name: string): DanceRating | undefined {
    const ds = environment!.fromName(name)!;
    return this.findDanceRatingById(ds.id);
  }

  public removeDanceRating(id: string): void {
    const index = this.danceRatings?.findIndex((dr) => dr.id === id);
    if (index === -1) {
      throw new Error(
        `Attempted to remove dancerating ${id} from song ${this.songId} but it didn't exist`
      );
    }
    this.danceRatings?.splice(index!, 1);
  }

  public findAlbum(
    album: string,
    trackNumber: number | undefined
  ): AlbumDetails | undefined {
    const name = album.toLowerCase();
    return this.albums?.find(
      (a) =>
        name === a.name?.toLowerCase() &&
        (!trackNumber || trackNumber === a.track)
    );
  }

  public get nextAlbumIndex(): number {
    const albums = this.albums;
    return albums && albums.length > 0
      ? albums[albums.length - 1].index! + 1
      : 0;
  }

  public get createdOrder(): string {
    return this.created ? timeOrder(this.created) : "U";
  }

  public get modifiedOrder(): string {
    return this.modified ? timeOrder(this.modified) : "U";
  }

  public get editedOrder(): string {
    return this.edited ? timeOrder(this.edited) : "U";
  }

  public get createdOrderVerbose(): string {
    return timeOrderVerbose(this.created);
  }

  public get modifiedOrderVerbose(): string {
    return timeOrderVerbose(this.modified);
  }

  public get editedOrderVerbose(): string {
    return this.edited ? timeOrderVerbose(this.edited) : "Undefined";
  }

  public get hasSample(): boolean {
    return !!this.sample && this.sample !== ".";
  }

  public get id(): string {
    return this.songId;
  }

  public get description(): string {
    return `"${this.title}" by ${this.artist}`;
  }

  public get categories(): string[] {
    return ["Tempo", "Other", "Music"];
  }

  public get hasDances(): boolean {
    const ratings = this.danceRatings;
    return !!ratings && ratings.length > 0;
  }

  public isCreator(user: string): boolean {
    const modified = this.getUserModified(user);
    return !!(modified && modified.isCreator);
  }

  public wasModifiedBy(user: string): boolean {
    return !!this.modifiedBy?.find((r) => r.userName === user);
  }

  public getUserModified(userName?: string): ModifiedRecord | undefined {
    if (!userName) {
      return undefined;
    }
    const name = userName.toLowerCase();
    return this.modifiedBy?.find((mr) => mr.userName.toLowerCase() === name);
  }

  public danceVote(danceId: string): boolean | undefined {
    const rating = this.findDanceRatingById(danceId);
    return rating
      ? TagList.build(this.currentUserTags).voteFromTags(rating?.positiveTag)
      : undefined;
  }

  private loadProperties(
    properties: SongProperty[],
    currentUser?: string
  ): void {
    let created = true;
    let creator = true;
    let user: string;
    let currentModified: ModifiedRecord;
    let deleted = false;
    let pseudo = false;

    properties.forEach((property) => {
      const baseName = property.baseName;

      switch (baseName) {
        case PropertyType.userField:
        case PropertyType.userProxy:
          user = property.value;
          currentModified = this.addModified(user, creator);
          pseudo = currentModified.isPseudo;
          break;
        case PropertyType.danceRatingField:
          this.addDanceRating(property.value);
          break;
        case PropertyType.addedTags:
          {
            const toAdd = this.getTaggableObject(property);
            if (toAdd) {
              toAdd.addTags(property.value, currentUser === user);
            }
          }
          break;
        case PropertyType.removedTags:
          {
            const toRem = this.getTaggableObject(property);
            if (toRem) {
              toRem.removeTags(property.value, currentUser === user);
            }
          }
          break;
        case PropertyType.deleteTag:
          this.forceDeleteTag(property.danceQualifier, property.value);
          break;

        case PropertyType.albumField:
        case PropertyType.publisherField:
        case PropertyType.trackField:
        case PropertyType.purchaseField:
          // All of these are taken care of with build album
          break;
        case PropertyType.deleteCommand:
          deleted = !!property.value || property.value.toLowerCase() === "true";
          break;
        case PropertyType.timeField:
          if (created) {
            this.modified = this.created = property.valueTyped as Date;
            created = false;
          } else {
            this.modified = property.valueTyped as Date;
          }
          if (!pseudo) {
            this.edited = property.valueTyped as Date;
          }
          break;
        case PropertyType.likeTag:
          if (currentModified) {
            currentModified.like = property.valueTyped as boolean | undefined;
          }
          break;
        case PropertyType.albumListField:
        case PropertyType.albumPromote:
        case PropertyType.albumOrder:
        case PropertyType.songId:
          // Obsolete fields
          break;
        case PropertyType.createCommand:
          creator = true;
          break;
        case PropertyType.editCommand:
          creator = false;
          break;
        default:
          if (!property.isAction) {
            /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
            (this as any)[pascalToCamel(baseName)] = property.valueTyped;
          }
          break;
      }
    });

    if (deleted) {
      this.clear();
    } else {
      this.albums = this.buildAlbumInfo(properties);
    }
  }

  private addModified(value: string, creator?: boolean): ModifiedRecord {
    const newRecord = ModifiedRecord.fromValue(value);
    let record = this.modifiedBy?.find(
      (r) => r.userName === newRecord.userName
    );
    if (!record) {
      if (!this.modifiedBy) {
        this.modifiedBy = [];
      }
      record = newRecord;
      this.modifiedBy.push(record);
    }
    record.isCreator = record.isCreator || creator;
    return record;
  }

  private addDanceRating(value: string): void {
    const drd = DanceRatingDelta.fromString(value);
    const ratings = this.danceRatings ?? [];
    const idx = ratings.findIndex((r) => r.id === drd.danceId);
    if (idx != -1) {
      const dr = ratings[idx];
      dr.weight += drd.delta;
      if (dr.weight <= 0) {
        ratings.splice(idx, 1);
      }
    } else if (drd.delta > 0) {
      const dr = new DanceRating({ danceId: drd.danceId, weight: drd.delta });
      if (this.danceRatings) {
        this.danceRatings.push(dr);
      } else {
        this.danceRatings = [dr];
      }
    }
  }

  private getTaggableObject(property: SongProperty): TaggableObject {
    const danceId = property.danceQualifier;
    return danceId ? this.findDanceRatingById(danceId)! : this;
  }

  private buildAlbumInfo(properties: SongProperty[]): AlbumDetails[] {
    const names = new Set<string>([
      PropertyType.albumField,
      PropertyType.publisherField,
      PropertyType.trackField,
      PropertyType.purchaseField,
    ]);

    const map = new Map<number, AlbumDetails>();
    let max = 0;

    properties
      .filter((p) => names.has(p.baseName) && p.hasIndex)
      .forEach((property) => {
        const name = property.baseName;
        const idx = property.index ?? 0;

        let details = map.get(idx);
        if (!details) {
          max = Math.max(max, idx);
          details = new AlbumDetails({ index: idx });
          map.set(idx, details);
        }

        const remove = !property.value;

        switch (name) {
          case PropertyType.albumField:
            details.name = remove ? undefined : property.value;
            break;
          case PropertyType.publisherField:
            details.publisher = remove ? undefined : property.value;
            break;
          case PropertyType.trackField:
            details.track = remove
              ? undefined
              : (property.valueTyped as number);
            break;
          case PropertyType.purchaseField:
            details.purchase.addId(property.qualifier!, property.value);
            break;
        }
      });

    const albums = [];
    for (let i = 0; i <= max; i++) {
      const album = map.get(i);
      if (album && album.name) {
        albums.push(album);
      }
    }
    return albums;
  }

  private clear(): void {
    this.title = "";
    this.artist = "";
    this.tempo = undefined;
    this.length = undefined;
    this.sample = undefined;
    this.danceability = undefined;
    this.energy = undefined;
    this.valence = undefined;
    this.danceRatings = [];
    this.modifiedBy = [];
    this.albums = [];
  }

  private forceDeleteTag(
    danceQualifier: string | undefined,
    value: string
  ): void {
    const taggable = danceQualifier
      ? this.findDanceRatingById(danceQualifier)
      : this;

    if (!taggable) {
      throw new Error(
        `Attempted to delete tag ${value} from ${danceQualifier} on ${this.songId}, but the dance rating doesn't exist`
      );
    }
    const tag = Tag.fromString(value);
    taggable.deleteTag(tag);

    if (tag.category === "Dance") {
      const ds = environment!.fromName(tag.value)!;
      this.removeDanceRating(ds.id);
    }
  }
}
