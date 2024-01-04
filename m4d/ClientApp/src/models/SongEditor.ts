import { DanceRatingDelta, DanceRatingVote, VoteDirection } from "./DanceRatingDelta";
import type { AxiosInstance } from "axios";
import type { DanceDatabase } from "./DanceDatabase";
import { Song } from "./Song";
import { SongHistory } from "./SongHistory";
import { PropertyType, type PropertyValue, SongProperty } from "./SongProperty";
import { TrackModel } from "./TrackModel";

declare const danceDatabase: DanceDatabase;

export class SongEditor {
  public songId: string;
  private properties: SongProperty[];
  private initialCount: number;
  private user?: string;
  public modified: boolean;
  public admin: boolean; // Requires put rather than patch
  public axios: AxiosInstance;
  // These are initializers to mollify linting, shouldn't be necessary
  public initialSong: Song = new Song();
  private initialHistory: SongHistory = new SongHistory();

  public constructor(axios: AxiosInstance, user?: string, history?: SongHistory) {
    history = history ? history : new SongHistory();
    this.songId = history.id;
    this.properties = [...history.properties];
    this.initialCount = history.properties.length;
    this.user = user;
    this.modified = false;
    this.admin = false;
    this.setInitials(history, user);
    this.axios = axios;
  }

  private setInitials(history: SongHistory, user?: string): void {
    this.initialSong = Song.fromHistory(history, user);
    this.initialHistory = new SongHistory({
      id: history.id,
      properties: [...history.properties],
    });
  }

  public get history(): SongHistory {
    return new SongHistory({ id: this.songId, properties: this.properties });
  }

  public get song(): Song {
    return Song.fromHistory(this.history, this.user);
  }

  public get userHasPreviousChanges(): boolean {
    if (!this.user) {
      throw new Error("Attempted to edit a song without a user");
    }
    return this.initialSong.wasModifiedBy(this.user);
  }

  public get editHistory(): SongHistory {
    const history = this.history;
    const init = this.initialCount;
    return new SongHistory({
      id: history.id,
      properties: history.properties.slice(init, history.properties.length),
    });
  }

  public get songHistory(): SongHistory {
    const history = this.history;
    return new SongHistory({
      id: history.id,
      properties: history.properties.slice(0, history.properties.length),
    });
  }

  public async saveExternalChanges(other: SongEditor): Promise<void> {
    // Verify that we're asking to save changes from a clone
    if (this.modified) {
      throw new Error("Attempting to add changes to an active editor");
    }
    if (this.initialCount !== other.initialCount) {
      throw new Error("Attempting to save from an unrelated editor");
    }

    other.editHistory.properties.forEach((p) => this.history.properties.push(p));

    return await this.saveChanges();
  }

  public async saveChanges(): Promise<void> {
    try {
      const admin = this.admin;
      const history = admin ? this.history : this.editHistory;
      const url = `/api/song/${history.id}`;
      if (admin) {
        await this.axios.put(url, history);
      } else {
        await this.axios.patch(url, history);
      }
      this.commit();
    } catch (e) {
      // eslint-disable-next-line no-console
      console.log(e);
      throw e;
    }
  }

  public async create(): Promise<void> {
    try {
      await this.axios.post("/api/song/", this.songHistory);
      this.commit();
    } catch (e) {
      // eslint-disable-next-line no-console
      console.log(e);
      throw e;
    }
  }

  public commit(): void {
    this.setInitials(this.history, this.user);
    this.modified = false;
    this.admin = false;
  }

  public revert(): void {
    this.properties = [...this.initialHistory.properties];
    this.modified = false;
    this.admin = false;
  }

  public get likeState(): boolean | undefined {
    const modifiedRecord = this.song.getUserModified(this.user);
    return modifiedRecord ? modifiedRecord.like : undefined;
  }

  public toggleLike(): void {
    const modifiedRecord = this.song.getUserModified(this.user);
    const like = this.rotateLike(modifiedRecord ? modifiedRecord.like : undefined);
    this.addProperty(PropertyType.likeTag, like);
  }

  public setLike(value?: boolean | null): void {
    let like = "null";
    switch (value) {
      case true:
        like = "true";
        break;
      case false:
        like = "false";
        break;
    }
    this.addProperty(PropertyType.likeTag, like);
  }

  private rotateLike(like?: boolean): string {
    switch (like) {
      case true:
        return "false";
      case false:
        return "null";
      default:
        return "true";
    }
  }

  public danceVote(vote: DanceRatingVote): void {
    switch (vote.direction) {
      case VoteDirection.Up:
        this.upVote(vote.danceId);
        break;
      case VoteDirection.Down:
        this.downVote(vote.danceId);
        break;
    }
  }

  private upVote(danceId: string): void {
    const vote = this.song.danceVote(danceId);
    let weight = 1;
    let positive: boolean | undefined = true;
    let negative: boolean | undefined;
    if (vote === true) {
      positive = false;
      weight = -1;
    } else if (vote === false) {
      negative = false;
      weight = 2;
    }
    this.setRatingProperty(danceId, weight);
    this.setVoteProperties(danceId, positive, negative);
  }

  private downVote(danceId: string): void {
    const vote = this.song.danceVote(danceId);
    let weight = -1;
    let negative: boolean | undefined = true;
    let positive: boolean | undefined;
    if (vote === true) {
      positive = false;
      weight = -2;
    } else if (vote === false) {
      negative = false;
      weight = 1;
    }

    this.setRatingProperty(danceId, weight);
    this.setVoteProperties(danceId, positive, negative);
  }

  private setRatingProperty(danceId: string, weight: number): void {
    this.addProperty(
      PropertyType.danceRatingField,
      new DanceRatingDelta(danceId, weight).toString(),
    );
  }

  private setVoteProperties(danceId: string, positive?: boolean, negative?: boolean): void {
    const posTag = `${danceDatabase.fromId(danceId)!.name}:Dance`;
    const negTag = "!" + posTag;

    if (positive === true) {
      this.addProperty(PropertyType.addedTags, posTag);
    } else if (positive === false) {
      this.addProperty(PropertyType.removedTags, posTag);
    }
    if (negative === true) {
      this.addProperty(PropertyType.addedTags, negTag);
    } else if (negative === false) {
      this.addProperty(PropertyType.removedTags, negTag);
    }
  }

  public addAlbumFromTrack(track: TrackModel): void {
    const album = this.song.findAlbum(track.album!, track.trackNumber);
    const index = album ? album.index! : this.song.nextAlbumIndex;
    if (!album) {
      this.addAlbumProperty(PropertyType.albumField, track.album, index);
      if (track) {
        this.addAlbumProperty(PropertyType.trackField, track.trackNumber, index);
      }
    }
    const service = track.serviceType[0].toUpperCase();
    this.addPurchaseProperty(track.trackId, index, service, "S");
    if (track.collectionId) {
      this.addPurchaseProperty(track.collectionId, index, service, "A");
    }
  }

  public addProperty(name: string, value: PropertyValue): SongProperty {
    this.modified = true;
    this.setupEdit();
    return this.createProperty(name, value);
  }

  public addAlbumProperty(name: string, value: PropertyValue, index = 0): SongProperty {
    return this.addProperty(`${name}:${index ?? 0}`, value);
  }

  public addPurchaseProperty(
    value: PropertyValue,
    index: number,
    service: string,
    type: string,
  ): SongProperty {
    return this.addProperty(
      `${PropertyType.purchaseField}:${index ?? 0}:${service.toUpperCase()}${type.toUpperCase()}`,
      value,
    );
  }

  public modifyProperty(name: string, value?: PropertyValue): SongProperty {
    this.modified = true;
    this.setupEdit();
    const property = this.findModified(name);
    if (!property) {
      return this.createProperty(name, value);
    }
    property.value = value ? value.toString() : "";
    return property;
  }

  public setupEdit(user?: string): void {
    user ??= this.user;
    const properties = this.properties;
    if (properties.length > this.initialCount || (user && this.isUserMerge(user))) {
      return;
    }

    this.createProperty(
      properties.length > 0 ? PropertyType.editCommand : PropertyType.createCommand,
    );

    this.createProperty(PropertyType.userField, user ?? this.user);

    this.createProperty(PropertyType.timeField, SongProperty.formatDate(new Date()));
  }

  private isUserMerge(user: string): boolean {
    const idx = this.lastActionIndex;
    if (idx === -1) {
      return false;
    }
    const props = this.properties;
    const merge = props[idx];
    if (merge.baseName !== PropertyType.mergeCommand) {
      return false;
    }
    for (let i = idx + 1; i < props.length; i++) {
      const prop = props[i];
      if (prop.baseName === PropertyType.userField && prop.value === user) {
        return true;
      }
    }
    return false;
  }

  // TODO: pull this when js/ts implement lastIndexOf
  private get lastActionIndex(): number {
    const props = this.properties;
    for (let i = props.length - 1; i >= 0; i--) {
      if (props[i].isAction) {
        return i;
      }
    }
    return -1;
  }

  public deleteProperty(index: number): void {
    const history = this.setupHistory(index);
    let count = 1;
    if (history[index].isAction) {
      while (index + count < history.length && !history[index + count].isAction) {
        count += 1;
      }
    }

    history.splice(index, count);
  }

  public promoteProperty(index: number): void {
    const prop = this.history.properties[index];
    this.addProperty(prop.name, prop.value);
  }

  public movePropertyUp(index: number): void {
    this.moveProperty(index, (index) => index - this.getPreviousSegmentSize(index));
  }

  public movePropertyDown(index: number): void {
    this.moveProperty(index, (index, size) => index + this.getSegmentSize(index + size));
  }

  public movePropertyFirst(index: number): void {
    this.moveProperty(index, (index, size) => (size === 1 ? this.getSegmentStart(index) + 1 : 0));
  }

  public movePropertyLast(index: number): void {
    this.moveProperty(index, (index, size) =>
      size === 1 ? this.getSegmentEnd(index) - 1 : this.history.properties.length - size,
    );
  }

  private moveProperty(index: number, computeInsert: (index: number, size: number) => number) {
    const history = this.setupHistory(index);
    const size = this.getSegmentSize(index);
    const insertAt = computeInsert(index, size);
    const removed = history.splice(index, size);
    history.splice(insertAt, 0, ...removed);
  }

  public insertProperty(index: number, prop: string): void {
    const history = this.setupHistory(index);
    history.splice(index, 0, SongProperty.FromString(prop));
  }

  private setupHistory(index: number): SongProperty[] {
    this.checkAdmin(index);
    this.modified = true;
    return this.properties;
  }

  private getSegmentSize(index: number): number {
    const properties = this.properties;
    if (index > properties.length) {
      return 0;
    }
    if (!properties[index].isAction) {
      return 1;
    }
    return this.getSegmentEnd(index + 1) - index;
  }

  private getPreviousSegmentSize(index: number): number {
    const properties = this.properties;
    if (index > properties.length) {
      return 0;
    }
    if (!properties[index].isAction) {
      return 1;
    }
    return index - this.getSegmentStart(index - 1);
  }

  private getSegmentStart(index: number): number {
    while (index > 0 && !this.properties[index].isAction) {
      index -= 1;
    }
    return index;
  }

  private getSegmentEnd(index: number): number {
    const properties = this.properties;
    while (index < properties.length && !this.properties[index].isAction) {
      index += 1;
    }
    return index;
  }

  private checkAdmin(index: number): void {
    if (index < this.initialCount) {
      this.admin = true;
    }
  }

  public adminEdit(properties: string): void {
    this.replaceAll(SongHistory.fromString(properties, this.songId).properties);
  }

  public replaceAll(properties: SongProperty[]): void {
    this.properties = properties;
    this.admin = true;
  }

  private createProperty(name: string, value?: PropertyValue): SongProperty {
    const property = new SongProperty({
      name: name,
      value: value ? value.toString() : undefined,
    });
    this.properties.push(property);
    return property;
  }

  private findModified(name: string): SongProperty | undefined {
    return this.modifiedProperties.find((p) => p.name === name);
  }

  private get modifiedProperties(): SongProperty[] {
    return this.properties.slice(this.initialCount);
  }
}
