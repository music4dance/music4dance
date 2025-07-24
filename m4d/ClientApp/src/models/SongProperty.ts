import { jsonMember, jsonObject } from "typedjson";

// Name Syntax:
//   BaseName[:idx[:qual]]
//     idx is zeros based index for multi-value fields (only album at this point?)
//     qual is a qualifier for purchase type (may generalize?)
//   Tag(+|-)[DanceQualifier]

export type PropertyValue = string | number | Date | boolean | undefined;

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

  // Comments
  addCommentField = "Comment+",
  removeCommentField = "Commnent-",

  // User/Song info
  ownerHash = "OwnerHash",
  likeTag = "Like",

  // Proxy Fields
  userProxy = "UserProxy",

  // Curator Fields
  deleteTag = "DeleteTag",

  // Azure Search Fields
  songIdField = "SongId",
  altIdField = "AlternateIds",
  moodField = "Mood",
  beatField = "Beat",
  albumsField = "Albums",
  createdField = "Create",
  editedField = "Edit",
  modifiedField = "Modified",
  dancesField = "Dances",
  usersField = "Users",
  genreTags = "GenreTags",
  tempoTags = "TempoTags",
  styleTags = "StyleTags",
  otherTags = "OtherTags",
  propertiesField = "Properties",
  serviceIds = "ServiceIds",

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
  @jsonMember(String) public name!: string;
  @jsonMember(String) public value!: string;

  public static BuildIndexName(baseName: string, index: number, modifier?: string): string {
    const idxString = index.toString().padStart(2, "0");
    const start = `${baseName}:${idxString}`;
    return modifier ? `${start}:${modifier}` : start;
  }

  public static FromString(s: string): SongProperty {
    const cells = s.split("=");
    return new SongProperty({
      name: cells[0],
      value: cells.length > 1 ? cells[1] : undefined,
    });
  }

  public constructor(init?: Partial<SongProperty>) {
    Object.assign(this, init);
  }

  public get valueTyped(): PropertyValue {
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
      case PropertyType.timeField:
        return this.dateValue;

      case PropertyType.likeTag:
        return this.booleanValue;

      default:
        return value;
    }
  }

  private get floatValue(): number | undefined {
    const n = Number.parseFloat(this.value);
    return Number.isNaN(n) ? undefined : n;
  }

  private get intValue(): number {
    return Number.parseInt(this.value, 10);
  }

  private get dateValue(): Date {
    // TODO:  Need to standardize on storing UTC time on the server (and in properties)
    return new Date(this.value);
  }

  private get booleanValue(): boolean | undefined {
    const value = this.value;
    if (value.toLowerCase() === "true") {
      return true;
    }
    if (value.toLowerCase() === "false") {
      return false;
    }
    return undefined;
  }

  public get baseName(): string {
    return this.parsePart(0)!;
  }

  public get hasIndex(): boolean {
    return this.hasPart(1);
  }

  public get safeIndex(): number | undefined {
    return this.computeIndex(true);
  }

  public get index(): number {
    return this.computeIndex(false)!;
  }

  private computeIndex(safe: boolean): number | undefined {
    const part = this.parsePart(1);
    if (part === undefined) {
      if (safe) {
        return undefined;
      }
      throw new Error(`Attempted to retrieve part ${1} from '${this.name}' which doesn't exist`);
    }

    const index = Number.parseInt(part, 10);
    if (Number.isNaN(index)) {
      if (safe) {
        return undefined;
      }
      throw new Error(`Index must be a number, not '${index}'`);
    }

    if (index < 0) {
      if (safe) {
        return undefined;
      }
      throw new Error(`Index must be a postitive integer, not '${index}'`);
    }

    return index;
  }

  public get danceQualifier(): string | undefined {
    return this.parsePart(1);
  }

  public get qualifier(): string | undefined {
    return this.parsePart(2);
  }

  public get isAction(): boolean {
    return this.name.startsWith(".");
  }

  public toString(): string {
    return `${this.name}=${this.value}`;
  }

  private hasPart(index: number): boolean {
    if (index === 0) {
      return true;
    }
    const first = this.name.indexOf(":");
    if (index === 1) {
      return first !== -1;
    }
    if (index === 2) {
      return first !== -1 && first !== this.name.lastIndexOf(":");
    }
    return false;
  }

  private parsePart(index: number): string | undefined {
    const parts = this.name.split(":");
    return parts.length > index ? parts[index] : undefined;
  }
}
