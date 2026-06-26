import { DanceRatingDelta } from "./DanceRatingDelta";
import { pascalToCamel } from "@/helpers/StringHelpers";
import { enumKeys } from "@/helpers/enumKeys";
import { timeOrder, timeOrderVerbose } from "@/helpers/timeHelpers";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { AlbumDetails } from "./AlbumDetails";
import { DanceRating } from "./DanceRating";
import { ModifiedRecord } from "./ModifiedRecord";
import { AmazonPurchaseInfo, PurchaseInfo, ServiceType } from "./Purchase";
import { SongHistory } from "./SongHistory";
import { PropertyType, SongProperty } from "./SongProperty";
import { Tag } from "./Tag";
import { TagList } from "./TagList";
import { TaggableObject } from "./TaggableObject";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";

@jsonObject
export class Song extends TaggableObject {
  public static fromHistory(history: SongHistory, currentUser?: string): Song {
    const song = new Song();
    song.songId = history.id;
    song.loadProperties(history.properties, currentUser);
    return song;
  }

  @jsonMember(String) public songId!: string;
  @jsonMember(String) public title!: string;
  @jsonMember(String) public artist!: string;
  @jsonMember(Number) public tempo?: number;
  @jsonMember(Number) public length?: number;
  @jsonMember(String) public sample?: string;
  @jsonMember(Number) public danceability?: number;
  @jsonMember(Number) public energy?: number;
  @jsonMember(Number) public valence?: number;
  @jsonMember(Date) public created!: Date;
  @jsonMember(Date) public modified!: Date;
  @jsonMember(Date) public edited?: Date;
  @jsonArrayMember(DanceRating) public danceRatings?: DanceRating[];
  @jsonArrayMember(ModifiedRecord) public modifiedBy?: ModifiedRecord[];
  @jsonArrayMember(AlbumDetails) public albums?: AlbumDetails[];

  private userModifiedProperties = new Set<string>();
  /** Maps field baseName → username of the last non-pseudo editor of that field. */
  private propLastSetByMap = new Map<string, string | undefined>();
  private hasExplicitSongTempo = false;
  private tempoInferredFromDance = false;
  /** danceId → whether the active per-dance tempo override was last set by a human. */
  private danceTempoUserModified = new Map<string, boolean>();
  /** danceId → username of the last human to set that dance's tempo override. */
  private danceTempoLastSetByMap = new Map<string, string | undefined>();

  public constructor(init?: Partial<Song>) {
    super();
    Object.assign(this, init);
  }

  /**
   * Check if a property was modified by a real user (not a bot)
   * @param field - Property field name (use PropertyType enum values)
   * @returns true if the property was set by a real user, false otherwise
   */
  public isUserModified(field: string): boolean {
    return this.userModifiedProperties.has(field);
  }

  /**
   * Returns the username of the last non-pseudo (human) user who set the given field,
   * or undefined if the field has never been set by a human.
   * @param field - Property field name (use PropertyType enum values)
   */
  public propLastSetBy(field: string): string | undefined {
    return this.propLastSetByMap.get(field);
  }

  /**
   * True when the effective tempo for a dance (override, or inherited song tempo when no
   * override exists) was last set by a human (non-pseudo) user, rather than algorithmically.
   */
  public isDanceTempoUserModified(danceId: string): boolean {
    const dr = this.findDanceRatingById(danceId);
    if (dr?.tempo != null) {
      return this.danceTempoUserModified.get(danceId) ?? false;
    }
    return this.isUserModified(PropertyType.tempoField);
  }

  /**
   * Username of the last human to set the effective tempo for a dance (the override's
   * setter, or the song tempo's setter when the dance has no override).
   */
  public danceTempoLastSetBy(danceId: string): string | undefined {
    const dr = this.findDanceRatingById(danceId);
    if (dr?.tempo != null) {
      return this.danceTempoLastSetByMap.get(danceId);
    }
    return this.propLastSetBy(PropertyType.tempoField);
  }

  /**
   * True only when the current song-level tempo came from dance-tempo promotion
   * (Tempo:DANCE=value) and no explicit song-level Tempo token has been applied.
   */
  public get isTempoInferredFromDance(): boolean {
    return this.tempoInferredFromDance;
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
    const ret: PurchaseInfo[] = [];

    for (const service of enumKeys(ServiceType)) {
      const serviceType = ServiceType[service];
      if (serviceType === ServiceType.Amazon) {
        // Always include Amazon as a search link regardless of stored ASIN (Option 1)
        const info = new AmazonPurchaseInfo({});
        info.artist = this.artist;
        info.songTitle = this.title;
        ret.push(info);
      } else {
        const purchase = this.getPurchaseInfo(serviceType);
        if (purchase) {
          ret.push(purchase);
        }
      }
    }

    return ret;
  }

  public findDanceRatingById(id: string): DanceRating | undefined {
    return this.danceRatings?.find((r) => r.id === id);
  }

  public findDanceRatingByName(name: string): DanceRating | undefined {
    const ds = safeDanceDatabase().fromName(name)!;
    return this.findDanceRatingById(ds.id);
  }

  /**
   * Returns the effective tempo in the context of an optional dance ID.
   * If a per-dance tempo override exists, it is preferred; otherwise song-level tempo is used.
   */
  public tempoForDance(danceId?: string): number | undefined {
    if (!danceId) {
      return this.tempo;
    }

    const rating = this.findDanceRatingById(danceId);
    return rating?.tempo ?? this.tempo;
  }

  public removeDanceRating(id: string): void {
    const index = this.danceRatings?.findIndex((dr) => dr.id === id);
    if (index === -1) {
      throw new Error(
        `Attempted to remove dancerating ${id} from song ${this.songId} but it didn't exist`,
      );
    }
    this.danceRatings?.splice(index!, 1);
  }

  public findAlbum(album: string, trackNumber: number | undefined): AlbumDetails | undefined {
    const name = album.toLowerCase();
    return this.albums?.find(
      (a) => name === a.name?.toLowerCase() && (!trackNumber || trackNumber === a.track),
    );
  }

  public get nextAlbumIndex(): number {
    const albums = this.albums;
    return albums && albums.length > 0 ? (albums[albums.length - 1]?.index ?? 0) + 1 : 0;
  }

  public get createdOrder(): string {
    return this.created ? timeOrder(this.created) : "N";
  }

  public get modifiedOrder(): string {
    return this.modified ? timeOrder(this.modified) : "N";
  }

  public get editedOrder(): string {
    return this.edited ? timeOrder(this.edited) : "N";
  }

  public get createdOrderVerbose(): string {
    return timeOrderVerbose(this.created);
  }

  public get modifiedOrderVerbose(): string {
    return timeOrderVerbose(this.modified);
  }

  public get editedOrderVerbose(): string {
    return this.edited ? timeOrderVerbose(this.edited) : "N";
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

  public get explicitDanceIds(): string[] {
    const tags = this.tags;
    return tags
      ? tags
          .filter(
            (t) => t.category === "Dance" && !t.value.startsWith("!") && !t.value.startsWith("-"),
          )
          .map((t) => safeDanceDatabase().fromName(t.value)!.id)
      : [];
  }

  public hasMeterTag(numerator: number): boolean {
    return !!this.tags.find((t) => t.key === `${numerator}/4:Tempo`);
  }

  public get numerator(): number | undefined {
    if (this.hasMeterTag(4)) {
      return 4;
    } else if (this.hasMeterTag(3)) {
      return 3;
    } else if (this.hasMeterTag(2)) {
      return 2;
    }
    return undefined;
  }

  private loadProperties(properties: SongProperty[], currentUser?: string): void {
    let created = true;
    let creator = true;
    let user: string;
    let currentModified: ModifiedRecord;
    let deleted = false;
    let pseudo = false;

    // Track each non-service user's net contribution per dance to enforce a ±1 cap.
    // Batch and service accounts (batch*, tempo-bot) are exempt and may use any delta.
    const userDanceContributions = new Map<string, number>();

    properties.forEach((property) => {
      const baseName = property.baseName;

      switch (baseName) {
        case PropertyType.userField:
        case PropertyType.userProxy:
          user = property.value;
          currentModified = this.addModified(user, creator);
          pseudo = currentModified.isPseudo;
          break;
        case PropertyType.danceRatingField: {
          const drd = DanceRatingDelta.fromString(property.value);
          const userName = currentModified?.userName;
          const isBatchUser = !userName || userName.startsWith("batch") || userName === "tempo-bot";
          if (!isBatchUser) {
            const key = `${userName}:${drd.danceId}`;
            const currentNet = userDanceContributions.get(key) ?? 0;
            const effectiveNet = Math.max(-1, Math.min(1, currentNet + drd.delta));
            const effectiveDelta = effectiveNet - currentNet;
            if (effectiveDelta !== 0) {
              userDanceContributions.set(key, effectiveNet);
              this.addDanceRating(effectiveDelta, drd.danceId);
            }
          } else {
            this.addDanceRating(drd.delta, drd.danceId);
          }
          break;
        }
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
        case PropertyType.tempoField: {
          // Tempo=value sets song tempo; Tempo:DANCEID=value sets per-dance override.
          // Empty value clears the field (null for dance, undefined for song).
          const danceIdQual = property.danceQualifier;
          if (danceIdQual) {
            const dr = this.findDanceRatingById(danceIdQual);
            if (dr) {
              const v = property.value;
              // Don't allow an algorithmic edit to clobber or clear a human's override.
              const wasUser = this.danceTempoUserModified.get(danceIdQual) ?? false;
              if (v) {
                const parsedTempo = Number(v);
                if (Number.isFinite(parsedTempo) && !(wasUser && pseudo)) {
                  dr.tempo = parsedTempo;
                  this.danceTempoUserModified.set(danceIdQual, !pseudo);
                  if (!pseudo && currentModified?.userName) {
                    this.danceTempoLastSetByMap.set(danceIdQual, currentModified.userName);
                  }
                  // Promote: if no song-level tempo has been set yet, infer it from this
                  // dance override. Preserves the semantic that the user is expressing a
                  // dance preference — other users remain free to set song.Tempo independently.
                  if (this.tempo == null && !this.hasExplicitSongTempo) {
                    this.tempo = parsedTempo;
                    this.tempoInferredFromDance = true;
                  }
                }
              } else if (!(wasUser && pseudo)) {
                dr.tempo = undefined; // empty = clear override
                this.danceTempoUserModified.delete(danceIdQual);
                this.danceTempoLastSetByMap.delete(danceIdQual);
              }
            }
          } else {
            // Song-level — same as default reflection path
            const value = property.valueTyped;
            if (value !== ".") {
              this.hasExplicitSongTempo = true;
              this.tempoInferredFromDance = false;
              const wasUser = this.userModifiedProperties.has(baseName);
              if (!(wasUser && pseudo)) {
                (this as any)[pascalToCamel(baseName)] = value;
                if (!pseudo) {
                  this.userModifiedProperties.add(baseName);
                  if (currentModified?.userName) {
                    this.propLastSetByMap.set(baseName, currentModified.userName);
                  }
                }
              }
            }
          }
          break;
        }
        case PropertyType.deleteTag:
          this.forceDeleteTag(property.danceQualifier, property.value);
          break;
        case PropertyType.addCommentField:
          {
            const toAdd = this.getTaggableObject(property);
            if (toAdd) {
              toAdd.addComment(property.value, user);
            }
          }
          break;
        case PropertyType.removeCommentField:
          {
            const toRem = this.getTaggableObject(property);
            if (toRem) {
              toRem.removeComment(user);
            }
          }
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
        case PropertyType.songIdField:
          // Obsolete fields
          break;
        case PropertyType.createCommand:
          creator = true;
          break;
        case PropertyType.editCommand:
          creator = false;
          break;
        default:
          {
            if (property.isAction) {
              break;
            }

            const value = property.valueTyped;
            if (value === ".") {
              break;
            }

            // Don't allow bot values to overwrite user values
            const wasUser = this.userModifiedProperties.has(baseName);
            if (wasUser && pseudo) {
              break;
            }

            /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
            (this as any)[pascalToCamel(baseName)] = value;

            if (!pseudo) {
              this.userModifiedProperties.add(baseName);
              if (currentModified?.userName) {
                this.propLastSetByMap.set(baseName, currentModified.userName);
              }
            }
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
    let record = this.modifiedBy?.find((r) => r.userName === newRecord.userName);
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

  private addDanceRating(delta: number, danceId: string): void {
    const ratings = this.danceRatings ?? [];
    const idx = ratings.findIndex((r) => r.id === danceId);
    if (idx != -1) {
      const dr = ratings[idx];
      if (dr) {
        dr.weight += delta;
        if (dr.weight <= 0) {
          ratings.splice(idx, 1);
        }
      }
    } else if (delta > 0) {
      const dr = new DanceRating({ danceId, weight: delta });
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
            details.track = remove ? undefined : (property.valueTyped as number);
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

  private forceDeleteTag(danceQualifier: string | undefined, value: string): void {
    const taggable = danceQualifier ? this.findDanceRatingById(danceQualifier) : this;

    if (!taggable) {
      throw new Error(
        `Attempted to delete tag ${value} from ${danceQualifier} on ${this.songId}, but the dance rating doesn't exist`,
      );
    }
    const tag = Tag.fromString(value);
    taggable.deleteTag(tag);

    if (tag.category === "Dance") {
      const ds = safeDanceDatabase()!.fromName(tag.value)!;
      this.removeDanceRating(ds.id);
    }
  }
}
