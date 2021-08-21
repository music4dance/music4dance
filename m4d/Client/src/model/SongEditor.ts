import {
  DanceRatingDelta,
  DanceRatingVote,
  VoteDirection,
} from "@/DanceRatingDelta";
import axios from "axios";
import { DanceEnvironment } from "./DanceEnvironmet";
import { Song } from "./Song";
import { SongHistory } from "./SongHistory";
import { PropertyType, PropertyValue, SongProperty } from "./SongProperty";
import { TrackModel } from "./TrackModel";

declare const environment: DanceEnvironment;

export class SongEditor {
  private songId: string;
  private properties: SongProperty[];
  private initialCount: number;
  private initialSong: Song;
  private user?: string;
  public modified: boolean;
  public admin: boolean; // Requires put rather than patch

  public constructor(user?: string, history?: SongHistory) {
    history = history ? history : new SongHistory();
    this.songId = history.id;
    this.properties = [...history.properties];
    this.initialCount = history.properties.length;
    this.user = user;
    this.modified = false;
    this.admin = false;
    this.initialSong = Song.fromHistory(this.history, this.user);
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

  public async saveChanges(): Promise<void> {
    try {
      const admin = this.admin;
      const history = admin ? this.history : this.editHistory;
      const url = `/api/song/${history.id}`;
      if (admin) {
        await axios.put(url, history);
      } else {
        await axios.patch(url, history);
      }
      this.commit();
    } catch (e) {
      console.log(e);
      throw e;
    }
  }

  public async create(): Promise<void> {
    try {
      await axios.post("/api/song/", this.songHistory);
      this.commit();
    } catch (e) {
      console.log(e);
      throw e;
    }
  }

  public commit(): void {
    this.initialCount = this.history.properties.length;
    this.initialSong = Song.fromHistory(this.history, this.user);
    this.modified = false;
    this.admin = false;
  }

  public revert(): void {
    this.properties.splice(
      this.initialCount,
      this.properties.length - this.initialCount
    );
    this.modified = false;
    this.admin = false;
  }

  public toggleLike(): void {
    const modifiedRecord = this.song.getUserModified(this.user);
    const like = this.rotateLike(
      modifiedRecord ? modifiedRecord.like : undefined
    );
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
      new DanceRatingDelta(danceId, weight).toString()
    );
  }

  private setVoteProperties(
    danceId: string,
    positive?: boolean,
    negative?: boolean
  ): void {
    const posTag = `${environment.fromId(danceId)!.danceName}:Dance`;
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
        this.addAlbumProperty(
          PropertyType.trackField,
          track.trackNumber,
          index
        );
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

  public addAlbumProperty(
    name: string,
    value: PropertyValue,
    index = 0
  ): SongProperty {
    return this.addProperty(`${name}:${index ?? 0}`, value);
  }

  public addPurchaseProperty(
    value: PropertyValue,
    index: number,
    service: string,
    type: string
  ): SongProperty {
    return this.addProperty(
      `${PropertyType.purchaseField}:${
        index ?? 0
      }:${service.toUpperCase()}${type.toUpperCase()}`,
      value
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
    const properties = this.properties;
    if (properties.length > this.initialCount) {
      return;
    }

    this.createProperty(
      properties.length > 0
        ? PropertyType.editCommand
        : PropertyType.createCommand
    );

    this.createProperty(PropertyType.userField, user ?? this.user);

    this.createProperty(
      PropertyType.timeField,
      SongProperty.formatDate(new Date())
    );
  }

  public deleteProperty(index: number): void {
    if (index < this.initialCount) {
      this.admin = true;
    }
    this.modified = true;
    const history = this.properties;
    let count = 1;
    if (history[index].isAction) {
      while (
        index + count < history.length &&
        !history[index + count].isAction
      ) {
        count += 1;
      }
    }

    history.splice(index, count);
  }

  public adminEdit(properties: string): void {
    const history = SongHistory.fromString(properties, this.songId);
    this.properties = history.properties;
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
