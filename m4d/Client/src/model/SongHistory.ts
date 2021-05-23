import "reflect-metadata";
import { jsonMember, jsonObject, jsonArrayMember } from "typedjson";
import { PropertyType, SongProperty } from "./SongProperty";
import { v4 as uuidv4 } from "uuid";
import { TrackModel } from "./TrackModel";
import { SongEditor } from "./SongEditor";

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
    editor.addAlbumProperty(PropertyType.albumField, track.album);
    if (track.trackNumber) {
      editor.addAlbumProperty(PropertyType.trackField, track.trackNumber);
    }
    if (track.trackId) {
      editor.addPurchaseProperty(track.trackId, 0, track.service[0], "S");
    }
    if (track.collectionId) {
      editor.addPurchaseProperty(track.collectionId, 0, track.service[0], "A");
    }
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

  private static serviceUserFromType(type: string): string {
    return `batch-${type[0].toLowerCase()}`;
  }

  private static parseProperties(s: string): SongProperty[] {
    const cells = s.split("\t");

    return cells.map(
      (c) =>
        new SongProperty({ name: c[0], value: c.length > 1 ? c[1] : undefined })
    );
  }
}
