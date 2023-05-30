import { AxiosInstance } from "axios";
import "reflect-metadata";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { v4 as uuidv4 } from "uuid";
import { Song } from "./Song";
import { SongChange } from "./SongChange";
import { SongEditor } from "./SongEditor";
import { PropertyType, SongProperty } from "./SongProperty";
import { TrackModel } from "./TrackModel";
import { UserQuery } from "./UserQuery";

interface SongRef {
  song: Song;
  index: number;
}

interface AlbumMap {
  name: string;
  index: number;
  songs: SongRef[];
}

@jsonObject
export class SongHistory {
  public static fromTrack(
    axios: AxiosInstance,
    track: TrackModel,
    currentUser?: string
  ): SongHistory {
    const editor = new SongEditor(axios, currentUser);
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

  public static merge(
    songId: string,
    songs: SongHistory[],
    user: string
  ): SongHistory {
    SongHistory.fixupAlbums(songs);
    const sorted = songs.sort(
      (a, b) => a.created.valueOf() - b.created.valueOf()
    );
    return new SongHistory({
      id: songId,
      properties: [
        ...sorted.flatMap((s) => s.annotated),
        new SongProperty({
          name: PropertyType.mergeCommand,
          value: sorted.map((s) => s.id).join(";"),
        }),
        new SongProperty({
          name: PropertyType.userField,
          value: user,
        }),
        new SongProperty({
          name: PropertyType.timeField,
          value: SongProperty.formatDate(new Date()),
        }),
      ],
    });
  }

  private static fixupAlbums(histories: SongHistory[]): void {
    const songs = histories.map((h) => Song.fromHistory(h));
    const albums: AlbumMap[] = [];

    // Build a list of unique albums with back-references to the song(s) they came from
    songs.forEach((s) => {
      s.albums?.forEach((a) => {
        let albumMap = albums.find(
          (x) =>
            SongHistory.normalizeString(x.name) ===
            SongHistory.normalizeString(a.name!)
        );
        if (!albumMap) {
          albumMap = {
            name: a.name ?? "Unknown Album",
            index: albums.length,
            songs: [],
          };
          albums.push(albumMap);
        }
        albumMap.songs.push({ song: s, index: a.index ?? 0 });
      });
    });

    histories.forEach((h) => h.fixupAlbums(albums));
  }

  private static normalizeString(str: string) {
    return str
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase();
  }

  private fixupAlbums(albums: AlbumMap[]): void {
    const idxMap = albums
      .filter((a) => !!a.songs.find((s) => s.song.songId === this.id))
      .map((a) => ({
        newIdx: a.index,
        oldIdx: a.songs.find((s) => s.song.songId === this.id)!.index,
      }));

    this.properties.forEach((p) => {
      const index = p.safeIndex;
      if (index !== undefined) {
        const map = idxMap.find((m) => m.oldIdx === index);
        if (!map) {
          throw Error("Failed to find album map in fixupAlbums");
        }
        if (map.newIdx !== map.oldIdx) {
          p.name = SongProperty.BuildIndexName(
            p.baseName,
            map.newIdx,
            p.qualifier
          );
        }
      }
    });
  }

  @jsonMember public id!: string; // GUID
  @jsonArrayMember(SongProperty) public properties!: SongProperty[];

  public constructor(init?: Partial<SongHistory>) {
    this.id = init?.id ?? uuidv4();
    this.properties = init?.properties ?? [];
  }

  private get changes(): SongChange[] {
    const track = [
      PropertyType.addedTags,
      PropertyType.removedTags,
      PropertyType.likeTag,
      PropertyType.addCommentField,
      PropertyType.removeCommentField,
      PropertyType.tempoField,
    ];
    return this.getChanges(track);
  }

  public get allChanges(): SongChange[] {
    return this.getChanges();
  }

  private getChanges(track?: string[]): SongChange[] {
    const changes: SongChange[] = [];

    const add = function (
      action: string,
      properties: SongProperty[],
      user?: string,
      date?: Date,
      actValue?: string
    ) {
      const last = changes.length > 1 ? changes[changes.length - 1] : undefined;
      if (
        last &&
        last.user === user &&
        date?.toDateString() === last.date?.toDateString()
      ) {
        last.properties = [...last.properties, ...properties];
      } else {
        changes.push(new SongChange(action, properties, user, date, actValue));
      }
    };

    let action: string | undefined = undefined;
    let actValue: string | undefined = undefined;
    let user: string | undefined = undefined;
    let date: Date | undefined = undefined;
    let properties: SongProperty[] = [];
    this.properties.forEach((property) => {
      if (property.isAction) {
        if (action) {
          add(action, properties, user, date, actValue);
          user = undefined;
          date = undefined;
          properties = [];
        }
        action = property.name.substring(1);
        actValue = property.value;
      } else {
        const baseName = property.baseName;
        switch (baseName) {
          case PropertyType.userField:
          case PropertyType.userProxy:
            user = property.valueTyped as string;
            break;
          case PropertyType.timeField:
            date = property.valueTyped as Date;
            break;
          default:
            if (!track || track.find((s) => s === baseName)) {
              properties.push(property);
            }
            break;
        }
      }
    });
    if (action) {
      add(action, properties, user, date, actValue);
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
            p.baseName === PropertyType.likeTag ||
            p.baseName === PropertyType.addCommentField ||
            p.baseName === PropertyType.removeCommentField ||
            p.baseName === PropertyType.tempoField
        )
    );
  }

  public get isSorted(): boolean {
    const changes = this.allChanges;
    const len = changes.length;
    if (len < 2) {
      return true;
    }
    for (let i = 0; i < len - 1; i++) {
      const a = changes[i];
      const b = changes[i + 1];
      if (a.date && b.date && a.date > b.date) {
        return false;
      }
    }
    return true;
  }

  public get sortedChanges(): SongChange[] {
    return this.allChanges.sort((a, b) => {
      if ((!a.date && b.date) || a.date == b.date) {
        return 0;
      } else if (!a.date || a.date < b.date!) {
        return -1;
      } else {
        return 1;
      }
    });
  }

  public get sorted(): SongProperty[] {
    return this.sortedChanges.flatMap((c) => c.propertyList);
  }

  public get isAnnotated(): boolean {
    return !this.properties.find(
      (p) =>
        (p.baseName === PropertyType.createCommand ||
          p.baseName === PropertyType.editCommand) &&
        !p.value
    );
  }

  public get annotated(): SongProperty[] {
    return this.sorted.map((p) =>
      p.baseName === PropertyType.createCommand ||
      p.baseName === PropertyType.editCommand
        ? new SongProperty({ name: p.name, value: this.id })
        : p
    );
  }

  public get created(): Date {
    const change = this.sortedChanges.find((c) => c.date);
    if (!change) {
      throw new Error("Unexpected song history with no valid date");
    }
    return change.date!;
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

  public latestComment(): SongChange | undefined {
    const changes = this.userChanges;
    return changes
      .reverse()
      .find((c) => c.properties.some((p) => p.baseName.startsWith("Comment")));
  }

  private static serviceUserFromType(type: string): string {
    return `batch-${type[0].toLowerCase()}`;
  }

  private static parseProperties(s: string): SongProperty[] {
    const rows = s.split("\t");

    return rows.map((row) => {
      return SongProperty.FromString(row);
    });
  }
}
