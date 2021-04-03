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

declare const environment: DanceEnvironment;

export class SongEditor {
  private history: SongHistory;
  private initialCount: number;
  private initialSong: Song;
  private user?: string;
  public modified: boolean;

  public constructor(user?: string, history?: SongHistory) {
    this.history = history ?? new SongHistory();
    this.initialCount = this.history.properties.length;
    this.user = user;
    this.modified = false;
    this.initialSong = Song.fromHistory(this.history, this.user);
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

  public async saveChanges(): Promise<void> {
    try {
      const history = this.editHistory;
      await axios.patch(`/api/song/${history.id}`, history);
      this.commit();
    } catch (e) {
      console.log(e);
      throw e;
    }
  }

  public commit(): void {
    this.initialCount = this.history.properties.length;
    this.modified = false;
  }

  public revert(): void {
    this.properties.splice(
      this.initialCount,
      this.properties.length - this.initialCount
    );
    this.modified = false;
  }

  private get properties(): SongProperty[] {
    return this.history.properties;
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

  public addProperty(name: string, value: PropertyValue): SongProperty {
    this.modified = true;
    this.setupEdit();
    return this.createProperty(name, value);
  }

  private setupEdit() {
    const properties = this.properties;
    if (properties.length > this.initialCount) {
      return;
    }

    this.createProperty(
      properties.length > 0
        ? PropertyType.editCommand
        : PropertyType.createCommand
    );

    this.createProperty(PropertyType.userField, this.user);

    this.createProperty(
      PropertyType.timeField,
      SongProperty.formatDate(new Date())
    );
  }

  private createProperty(name: string, value?: PropertyValue): SongProperty {
    const property = new SongProperty({
      name: name,
      value: value ? value.toString() : undefined,
    });
    this.properties.push(property);
    return property;
  }
}
