import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";

// Name Syntax:
//   BaseName[:idx[:qual]]
//     idx is zeros based index for multi-value fields (only album at this point?)
//     qual is a qualifier for purchase type (may generalize?)
//   Tag(+|-)[DanceQualifier]

// TODO:
//  Build Song from SongHistory:
//   TaggableObject (add to tags and song)
//   DanceRating
//   Tags Add/Remove
//   Albums

// yarn jest SongProperty.tests.ts

export enum PropertyType {
  // Field names - note that these must be kept in sync with the actual property names
  userField = "User",
  timeField = "Time",
  titleField = "Title",
  artistField = "Artist",
  tempoField = "Tempo",
  lengthField = "Length",
  sampleField = "Sample",
  danceabilityField = "Danceability",
  energyField = "Energy",
  valenceFiled = "Valence",
  titleHashField = "TitleHash",

  // Album Fields
  albumField = "Album",
  publisherField = "Publisher",
  trackField = "Track",
  purchaseField = "Purchase",
  albumListField = "AlbumList",
  albumPromote = "PromoteAlbum",
  albumOrder = "OrderAlbums",

  // Dance Rating
  danceRatingField = "DanceRating",

  // Tags
  addedTags = "Tag+",
  removedTags = "Tag-",

  // User/Song info
  ownerHash = "OwnerHash",
  likeTag = "Like",

  // Proxy Fields
  userProxy = "UserProxy",

  // Azure Search Fields
  songIdField = "SongId",
  altIdField = "AlternateIds",
  moodField = "Mood",
  beatField = "Beat",
  albumsField = "Albums",
  createdField = "Created",
  modifiedField = "Modified",
  dancesField = "Dances",
  usersField = "Users",
  danceTagsInferred = "DanceTagsInferred",
  genreTags = "GenreTags",
  tempoTags = "TempoTags",
  styleTags = "StyleTags",
  otherTags = "OtherTags",
  propertiesField = "Properties",
  serviceIds = "ServiceIds",
  lookupStatus = "LookupStatus",

  // Special cases for reading scraped data
  titleArtistCell = "TitleArtist",
  dancersCell = "Dancers",
  danceTags = "DanceTags",
  songTags = "SongTags",
  measureTempo = "MPM",
  multiDance = "MultiDance",

  // Commands
  createCommand = ".Create",
  editCommand = ".Edit",
  deleteCommand = ".Delete",
  mergeCommand = ".Merge",
  undoCommand = ".Undo",
  redoCommand = ".Redo",
  failedLookup = ".FailedLookup",
  noSongId = ".NoSongId", // Pseudo action for serialization
  serializeDeleted = ".SerializeDeleted", // Pseudo action for serialization

  successResult = ".Success",
  failResult = ".Fail",
  messageData = ".Message",
}

@jsonObject
export class SongProperty {
  @jsonMember public name!: string;
  @jsonMember public value!: string;

  public get valueTyped(): string | number | Date | boolean | undefined {
    const value = this.value;
    switch (this.baseName) {
      // decimal & float
      case PropertyType.tempoField:
      case PropertyType.danceabilityField:
      case PropertyType.valenceFiled:
      case PropertyType.energyField:
        return this.floatValue;

      // int
      case PropertyType.lengthField:
      case PropertyType.trackField:
      case PropertyType.ownerHash:
        return this.intValue;

      // date
      case PropertyType.createdField:
      case PropertyType.modifiedField:
        return this.dateValue;

      case PropertyType.likeTag:
        return this.booleanValue;

      default:
        return value;
    }
  }

  private get floatValue(): number {
    return Number.parseFloat(this.value);
  }

  private get intValue(): number {
    return Number.parseInt(this.value, 10);
  }

  private get dateValue(): Date {
    return new Date(this.value);
  }

  private get booleanValue(): boolean | undefined {
    const value = this.value;
    if (value === "true") {
      return true;
    }
    if (value === "false") {
      return false;
    }
    return undefined;
  }

  public get baseName(): string {
    return this.parsePart(0);
  }

  public get index(): number {
    const index = Number.parseInt(this.parsePart(1), 10);
    if (Number.isNaN(index)) {
      throw new Error(`Index must be a number, not '${index}'`);
    }
    if (index < 0) {
      throw new Error(`Index must be a postitive integer, not '${index}'`);
    }
    return index;
  }

  public get danceQualifier(): string {
    return this.parsePart(1);
  }

  public get isAction(): boolean {
    return this.name.startsWith(".");
  }

  private parsePart(index: number) {
    const parts = this.name.split(":");
    if (parts.length > index) {
      return parts[index];
    }
    throw new Error(
      `Attempted to retrieve part ${index} from '${this.name}' which doesn't exist`
    );
  }
}
