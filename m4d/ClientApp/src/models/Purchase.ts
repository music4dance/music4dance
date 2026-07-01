import { enumKeys } from "@/helpers/enumKeys";
import { jsonMember, jsonObject } from "typedjson";

export enum ServiceType {
  Amazon = "a",
  ITunes = "i",
  Spotify = "s",
}

export enum ServiceObjectType {
  Album = "a",
  Song = "s",
}

@jsonObject
export class PurchaseInfo {
  public static Build(
    service: ServiceType,
    albumId?: string,
    songId?: string,
  ): SpotifyPurchaseInfo {
    const info = { albumId, songId };
    switch (service.toLowerCase()) {
      case ServiceType.Amazon:
        return new AmazonPurchaseInfo(info);
      case ServiceType.ITunes:
        return new ItunesPurchaseInfo(info);
      case ServiceType.Spotify:
        return new SpotifyPurchaseInfo(info);
      default:
        throw new Error(`Invalid Purchase type ${service}`);
    }
  }

  public static NamesFromFilter(filter: string | undefined): string[] {
    if (!filter) {
      return [];
    }

    const ret = [];

    const f = filter.toLowerCase();
    for (const service of enumKeys(ServiceType)) {
      const value = ServiceType[service];
      if (f.indexOf(value) !== -1) {
        ret.push(service);
      }
    }

    return ret;
  }

  public get service(): ServiceType {
    throw new Error("Unimplemented");
  }

  public get link(): string {
    throw new Error("Unimplemented");
  }

  public get name(): string {
    throw new Error("Unimplemented");
  }

  public get logo(): string {
    return `/images/icons/${this.name}-logo.png`;
  }

  public get charm(): string {
    return `/images/icons/${this.name}-charm.png`;
  }

  public get altText(): string {
    return `Find it on ${this.name}`;
  }

  @jsonMember(String) public albumId!: string;
  @jsonMember(String) public songId!: string;

  public constructor(init?: Partial<PurchaseInfo>) {
    Object.assign(this, init);
  }
}

@jsonObject
export class ItunesPurchaseInfo extends PurchaseInfo {
  public get service(): ServiceType {
    return ServiceType.ITunes;
  }

  public get link(): string {
    return `https://itunes.apple.com/album/id${this.albumId}?i=${this.songId}&uo=4&at=11lwtf`;
  }

  public get name(): string {
    return "ITunes";
  }
}

@jsonObject
export class AmazonPurchaseInfo extends PurchaseInfo {
  /** Song title — set by Song.getPurchaseInfos() after construction; not serialized. */
  public songTitle?: string;
  /** Artist name — set by Song.getPurchaseInfos() after construction; not serialized. */
  public artist?: string;

  public get service(): ServiceType {
    return ServiceType.Amazon;
  }

  public get link(): string {
    const q = encodeURIComponent(`${this.artist ?? ""} ${this.songTitle ?? ""}`.trim());
    if (q) {
      return `https://www.amazon.com/s?i=digital-music&k=${q}&tag=msc4dnc-20`;
    }
    // Fallback: if artist/songTitle were not populated (e.g. via PurchaseEncoded.decode()),
    // use the stored ASIN as a direct product link rather than generating an empty search.
    if (this.songId) {
      return `https://www.amazon.com/dp/${this.cleanSong}?tag=msc4dnc-20`;
    }
    return `https://www.amazon.com/s?i=digital-music&tag=msc4dnc-20`;
  }

  public get name(): string {
    return "Amazon";
  }

  // cleanSong / cleanAlbum retained for potential future use (Option 4 direct-link fallback)
  public get cleanAlbum(): string {
    return this.cleanId(this.albumId);
  }

  public get cleanSong(): string {
    return this.cleanId(this.songId);
  }

  private cleanId(id: string): string {
    if (id.startsWith("D:") || id.startsWith("A:")) {
      return id.substring(2);
    }
    return id;
  }
}

@jsonObject
export class SpotifyPurchaseInfo extends PurchaseInfo {
  public get service(): ServiceType {
    return ServiceType.Spotify;
  }

  public get link(): string {
    return `https://open.spotify.com/track/${this.songId}`;
  }

  public get name(): string {
    return "Spotify";
  }
}

@jsonObject
export class PurchaseEncoded {
  @jsonMember(String, { name: "aa" }) public aa?: string;
  @jsonMember(String, { name: "as" }) public as?: string;
  @jsonMember(String, { name: "ia" }) public ia?: string;
  @jsonMember(String, { name: "is" }) public is?: string;
  @jsonMember(String, { name: "sa" }) public sa?: string;
  @jsonMember(String, { name: "ss" }) public ss?: string;

  // Accumulated ids from the SongProperty stream (history / buildAlbumInfo path).
  // The primary id is the first-added one; the JSON API path uses @jsonMember fields instead.
  private _ids = new Map<string, string[]>();

  public getId(service: ServiceType, obj: ServiceObjectType): string | undefined {
    const key = service.toLowerCase() + obj.toLowerCase();
    // History path: return the primary (first) accumulated id.
    const accumulated = this._ids.get(key);
    if (accumulated && accumulated.length > 0) {
      return accumulated[0];
    }
    // JSON API path: fall back to the deserialized property.
    /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
    return this.cleanId((this as any)[key]);
  }

  public decode(): PurchaseInfo[] {
    const ret = [];

    for (const service of enumKeys(ServiceType)) {
      const purchase = this.decodeService(ServiceType[service]);
      if (purchase) {
        ret.push(purchase);
      }
    }

    return ret;
  }

  public decodeService(service: ServiceType): PurchaseInfo | undefined {
    const song = this.getId(service, ServiceObjectType.Song);
    if (!song) {
      return undefined;
    }
    const album = this.getId(service, ServiceObjectType.Album);

    return PurchaseInfo.Build(service, album, song);
  }

  /**
   * Adds a single id to the slot for `type` (e.g. "ss", "ia"). Each call is additive —
   * a second call for the same type accumulates alongside the first rather than replacing it.
   * The primary id (used for links) is always the first one added.
   */
  public addId(type: string, id: string): void {
    if (type.length !== 2) {
      throw new Error(`Invalid service type ${type}`);
    }
    if (!id) {
      return;
    }
    const key = type.toLowerCase();
    const existing = this._ids.get(key) ?? [];
    if (!existing.includes(id)) {
      this._ids.set(key, [...existing, id]);
    }
  }

  /**
   * Removes a specific id from the accumulated slot for `type`. No-ops if the id is not
   * present. When the last id is removed the slot becomes empty and getId returns undefined.
   */
  public removeId(type: string, id: string): void {
    const key = type.toLowerCase();
    const existing = this._ids.get(key);
    if (!existing) {
      return;
    }
    this._ids.set(key, existing.filter((x) => x !== id));
  }

  private cleanId(id: string): string | undefined {
    if (!id) {
      return;
    }
    const idx = id.indexOf("[");
    if (idx > 0) {
      return id.substring(0, idx);
    }
    return id;
  }
}
