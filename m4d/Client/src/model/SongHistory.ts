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
