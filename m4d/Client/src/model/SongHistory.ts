import "reflect-metadata";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { v4 as uuidv4 } from "uuid";
import { SongChange } from "./SongChange";
import { SongEditor } from "./SongEditor";
import { PropertyType, SongProperty } from "./SongProperty";
import { TrackModel } from "./TrackModel";
import { UserQuery } from "./UserQuery";

@jsonObject
export class SongHistory {
  public static fromTrack(
    track: TrackModel,
    currentUser?: string
  ): SongHistory {
    const editor = new SongEditor(currentUser);
    // Give this user the credit for creating the song
    if (currentUser) {
      editor.setupEdit();
    }

    // Attribute the initial edits to the service
    editor.setupEdit(this.serviceUserFromType(track.service));
    editor.addProperty(PropertyType.titleField, track.name);
    editor.addProperty(PropertyType.artistField, track.artist);
    editor.addAlbumFromTrack(track);
    if (track.genres) {
      editor.addProperty(
        PropertyType.addedTags,
        track.genres.map((t) => `${t}:Music`).join("|")
      );
    }
    if (track.sampleUrl) {
      editor.addProperty(PropertyType.sampleField, track.sampleUrl);
    }

    return editor.songHistory;
  }

  public static fromString(s: string, songId?: string): SongHistory {
    songId = songId ?? uuidv4();
    return new SongHistory({
      id: songId,
      properties: SongHistory.parseProperties(s),
    });
  }

  @jsonMember public id!: string; // GUID
  @jsonArrayMember(SongProperty) public properties!: SongProperty[];

  public constructor(init?: Partial<SongHistory>) {
    this.id = init?.id ?? uuidv4();
    this.properties = init?.properties ?? [];
  }

  public get changes(): SongChange[] {
    const changes: SongChange[] = [];

    const add = function (
      action: string,
      properties: SongProperty[],
      user?: string,
      date?: Date
    ) {
      const last = changes.length > 1 ? changes[changes.length - 1] : undefined;
      if (
        last &&
        last.user === user &&
        date?.toDateString() === last.date?.toDateString()
      ) {
        last.properties = [...last.properties, ...properties];
      } else {
        changes.push(new SongChange(action, properties, user, date));
      }
    };

    let action: string | undefined = undefined;
    let user: string | undefined = undefined;
    let date: Date | undefined = undefined;
    let properties: SongProperty[] = [];
    this.properties.forEach((property) => {
      if (property.isAction) {
        if (action) {
          add(action, properties, user, date);
          user = undefined;
          date = undefined;
          properties = [];
        }
        action = property.name.substr(1);
      } else
        switch (property.baseName) {
          case PropertyType.userField:
          case PropertyType.userProxy:
            user = property.valueTyped as string;
            break;
          case PropertyType.timeField:
            date = property.valueTyped as Date;
            break;
          case PropertyType.addedTags:
          case PropertyType.removedTags:
          case PropertyType.likeTag:
            properties.push(property);
            break;
        }
    });
    if (action) {
      add(action, properties, user, date);
    }

    return changes;
  }

  public Deanonymize(userName: string, userId: string): SongHistory {
    return new SongHistory({
      id: this.id,
      properties: this.properties.map((p) =>
        p.baseName === PropertyType.userField && p.value.indexOf(userId) != -1
          ? new SongProperty({
              name: p.name,
              value: p.value.replace(userId, userName),
            })
          : p
      ),
    });
  }

  public get userChanges(): SongChange[] {
    return this.changes.filter(
      (c) =>
        c.user &&
        !c.isBatch &&
        !!c.properties.find(
          (p) =>
            p.baseName === PropertyType.addedTags ||
            p.baseName === PropertyType.removedTags ||
            p.baseName === PropertyType.likeTag
        )
    );
  }

  public singleUserChanges(user: string): SongChange[] {
    const cleanUser = new UserQuery(user).userName;
    return this.userChanges.filter(
      (c) => new UserQuery(c.user).userName === cleanUser
    );
  }

  public recentUserChange(user: string): SongChange | undefined {
    const changes = this.singleUserChanges(user);
    if (changes.length === 0) {
      return undefined;
    }
    return changes[changes.length - 1];
  }

  public latestChange(): SongChange | undefined {
    const changes = this.userChanges;
    if (changes.length === 0) {
      return undefined;
    }
    return changes[changes.length - 1];
  }

  private static serviceUserFromType(type: string): string {
    return `batch-${type[0].toLowerCase()}`;
  }

  private static parseProperties(s: string): SongProperty[] {
    const rows = s.split("\t");

    return rows.map((row) => {
      const cells = row.split("=");
      return new SongProperty({
        name: cells[0],
        value: cells.length > 1 ? cells[1] : undefined,
      });
    });
  }
}
