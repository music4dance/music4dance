import type {
  SharedIdAction,
  SharedIdAdditionRequest,
  SharedIdChoiceRequest,
  SharedIdOwnerProposal,
  SharedIdOwnersResponse,
  SharedIdRemovalRequest,
  SharedIdReviewCluster,
  SharedIdReviewSong,
  SharedIdServiceTrack,
} from "./SharedIdReviewModel";

// The names (and tempo) a merged song takes, each from one of the cluster's songs or, as null,
// from the If merged column's proposal (the feat. cleanup of the merge; for the tempo, what the
// merge gives)
export type NameField = "title" | "artist" | "artists" | "tempo";

export const nameFields: NameField[] = ["title", "artist", "artists", "tempo"];

/** Individual artists as typed in the If merged box, "X | Y", to their list. */
export function parseArtists(text: string): string[] {
  return text
    .split("|")
    .map((a) => a.trim())
    .filter((a) => a.length > 0);
}

/** A tempo as typed in the If merged box, or null when it isn't a positive number. */
export function parseTempo(text: string): number | null {
  const tempo = Number(text.trim());
  return text.trim() !== "" && Number.isFinite(tempo) && tempo > 0 ? tempo : null;
}

// "Leave out" in a song's group select
export const leaveOut = "";

/** The groups a cluster's songs can be merged in: one per song, A, B, … */
export function groupNames(songCount: number): string[] {
  return Array.from({ length: songCount }, (_, i) => String.fromCharCode(65 + i));
}

/** The songs' Spotify and iTunes ids, once each: the ones POST /api/sharedids/tracks looks up. */
export function lookupIds(songs: SharedIdReviewSong[]): string[] {
  const ids = songs.flatMap((s) => s.albums.flatMap((a) => a.ids.map((i) => i.id)));
  return [...new Set(ids.filter((id) => id.startsWith("S:") || id.startsWith("I:")))];
}

function albumKey(songId: string, album: number): string {
  return `${songId}|${album}`;
}

function idKey(songId: string, album: number, id: string): string {
  return `${songId}|${album}|${id}`;
}

function additionKey(addition: SharedIdAdditionRequest): string {
  return `${addition.songId}|${addition.id}`;
}

/**
 * What the reviewer has chosen for one cluster of the shared-id review page. Starts from the
 * cluster's defaults: its default action, every song in group A, every name from the proposal
 * (or the first song when there's none), nothing deleted and nothing removed. The server re-checks everything on
 * Apply (SharedIdLog.Apply); this only collects the choice and says which controls apply.
 *
 * Wrap an instance in reactive() to use it in a component: its state is plain fields, a Map and
 * a Set, which Vue tracks.
 */
export class ClusterChoice {
  public action: SharedIdAction;
  public readonly groups = new Map<string, string>();
  public readonly names: Record<NameField, string | null>;
  // What the reviewer typed over the proposal's names in the If merged column, or null for the
  // proposal as it is
  public readonly typed: Record<NameField, string | null> = {
    title: null,
    artist: null,
    artists: null,
    tempo: null,
  };
  public readonly albumRemovals = new Set<string>();
  public readonly idRemovals = new Set<string>();
  // The ids Sort out ids would give a song that doesn't have them, and the ones checked
  public proposedAdditions: SharedIdAdditionRequest[] = [];
  public readonly additions = new Set<string>();
  public readonly deletions = new Set<string>();
  // What Spotify and iTunes say the ids are, once looked up
  public tracks?: Map<string, SharedIdServiceTrack>;
  // Which song each shared id belongs to, once Sort out ids has run
  public owners?: SharedIdOwnerProposal[];

  public constructor(public readonly cluster: SharedIdReviewCluster) {
    this.action = cluster.defaultAction;
    cluster.songs.forEach((s) => this.groups.set(s.songId, "A"));
    this.names = ClusterChoice.defaultNames(cluster);
  }

  private static defaultNames(cluster: SharedIdReviewCluster): Record<NameField, string | null> {
    const from = cluster.proposal ? null : (cluster.songs[0]?.songId ?? null);
    return { title: from, artist: from, artists: from, tempo: from };
  }

  public setAction(action: SharedIdAction): void {
    this.action = action;
  }

  public get hasSongs(): boolean {
    return this.cluster.songs.length > 0;
  }

  public get groupNames(): string[] {
    return groupNames(this.cluster.songs.length);
  }

  /** The song's group; a song to delete is left out, whatever group it was in. */
  public groupOf(songId: string): string {
    return this.isDeleted(songId) ? leaveOut : (this.groups.get(songId) ?? leaveOut);
  }

  /**
   * Puts the song in a group, or leaves it out. Grouping the songs is choosing what to merge, so
   * the action becomes Merge groups: leaving out the one song of a cluster that started as Leave
   * for later (its length is off, say) merges the rest and lets them name the merge.
   */
  public setGroup(songId: string, group: string): void {
    this.groups.set(songId, group);
    this.action = "Merge";
    if (group === leaveOut) {
      this.resetNamesFrom(songId);
    }
  }

  // A song left out of every group can't name a merged song
  private resetNamesFrom(songId: string): void {
    nameFields
      .filter((f) => this.names[f] === songId)
      .forEach((f) => (this.names[f] = ClusterChoice.defaultNames(this.cluster)[f]));
  }

  public isDeleted(songId: string): boolean {
    return this.deletions.has(songId);
  }

  /**
   * Deletes the song on Apply, whatever the action. It's left out of every group while it's to
   * be deleted, and gets its group back if it isn't any more.
   */
  public setDeleted(songId: string, deleted: boolean): void {
    if (deleted) {
      this.deletions.add(songId);
      this.resetNamesFrom(songId);
    } else {
      this.deletions.delete(songId);
    }
  }

  /** The songs aren't all in one group: the If merged column no longer shows what Apply makes. */
  public get isSplit(): boolean {
    return new Set(this.cluster.songs.map((s) => this.groupOf(s.songId))).size > 1;
  }

  public get isMerge(): boolean {
    return this.action === "Merge";
  }

  /** A name can come from a song only while the song is in a group that's merged. */
  public canName(songId: string): boolean {
    return this.isMerge && this.groupOf(songId) !== leaveOut;
  }

  public pickName(field: NameField, songId: string | null): void {
    if (songId === null || this.canName(songId)) {
      this.names[field] = songId;
    }
  }

  /**
   * The If merged column's names can be typed over only when the songs are merged in one group:
   * a name typed for two merged songs would fit at most one of them.
   */
  public get canType(): boolean {
    return this.isMerge && !this.isSplit;
  }

  /**
   * Types over the proposal's name, which picks it: Apply names the merged song with the text.
   * Text the same as the proposal's (or blank) is the proposal again.
   */
  public typeName(field: NameField, text: string, proposed?: string): void {
    if (!this.canType) {
      return;
    }
    this.names[field] = null;
    this.typed[field] = text.trim() === "" || text === (proposed ?? "") ? null : text;
  }

  public isAlbumRemoved(songId: string, album: number): boolean {
    return this.albumRemovals.has(albumKey(songId, album));
  }

  public setAlbumRemoved(songId: string, album: number, removed: boolean): void {
    const key = albumKey(songId, album);
    if (removed) {
      this.albumRemovals.add(key);
    } else {
      this.albumRemovals.delete(key);
    }
  }

  /** The id is checked for removal, or its album is: either way it goes. */
  public isIdRemoved(songId: string, album: number, id: string): boolean {
    return this.isAlbumRemoved(songId, album) || this.idRemovals.has(idKey(songId, album, id));
  }

  public setIdRemoved(songId: string, album: number, id: string, removed: boolean): void {
    const key = idKey(songId, album, id);
    if (removed) {
      this.idRemovals.add(key);
    } else {
      this.idRemovals.delete(key);
    }
  }

  /** The addition is checked, and neither song is to be deleted. */
  public isAdded(addition: SharedIdAdditionRequest): boolean {
    return (
      this.additions.has(additionKey(addition)) &&
      !this.isDeleted(addition.songId) &&
      !this.isDeleted(addition.fromSong)
    );
  }

  public setAdded(addition: SharedIdAdditionRequest, added: boolean): void {
    if (added) {
      this.additions.add(additionKey(addition));
    } else {
      this.additions.delete(additionKey(addition));
    }
  }

  /** The ids Sort out ids would give this song, with the album each comes from. */
  public additionsTo(songId: string): { addition: SharedIdAdditionRequest; album?: string }[] {
    return this.proposedAdditions
      .filter((a) => a.songId === songId)
      .map((a) => ({
        addition: a,
        album: this.cluster.songs
          .find((s) => s.songId === a.fromSong)
          ?.albums.find((al) => al.index === a.fromAlbum)?.name,
      }));
  }

  public setTracks(tracks: SharedIdServiceTrack[]): void {
    this.tracks = new Map(tracks.map((t) => [t.id, t]));
  }

  /** "song 2" for the cluster's second song. */
  public songLabel(songId?: string): string {
    const index = this.cluster.songs.findIndex((s) => s.songId === songId);
    return index < 0 ? "a song" : `song ${index + 1}`;
  }

  /**
   * Probably different songs: lengths too far apart, titles or artists that are only close or
   * differ, a rejected pair or a .NoMerge song. Sort out ids on this page runs for these.
   */
  public get needsSorting(): boolean {
    const c = this.cluster;
    return (
      this.hasSongs &&
      lookupIds(c.songs).length > 0 &&
      (!!c.lengthsDiffer ||
        c.match === "Close" ||
        c.match === "Different" ||
        c.hasRejectedPair ||
        c.hasNoMergeSong)
    );
  }

  /**
   * Takes in what Sort out ids proposes: checks the removals and additions that give each id to
   * its song, and fills the ids' tooltips. When something changes the cluster becomes Leave for
   * later, so Apply doesn't merge the songs it has just separated.
   */
  public applyOwners(response: SharedIdOwnersResponse): void {
    this.owners = response.ids;
    this.setTracks(response.tracks);
    response.removals.forEach((r) =>
      r.id === undefined || r.id === null
        ? this.setAlbumRemoved(r.songId, r.album, true)
        : this.setIdRemoved(r.songId, r.album, r.id, true),
    );
    this.proposedAdditions = response.additions ?? [];
    this.proposedAdditions.forEach((a) => this.setAdded(a, true));
    if (response.removals.length > 0 || this.proposedAdditions.length > 0) {
      this.action = "Leave";
    }
  }

  /** The choice differs from the cluster's defaults. */
  public get isDirty(): boolean {
    const defaults = ClusterChoice.defaultNames(this.cluster);
    return (
      this.action !== this.cluster.defaultAction ||
      this.cluster.songs.some((s) => this.groupOf(s.songId) !== "A") ||
      nameFields.some((f) => this.names[f] !== defaults[f] || this.typed[f] !== null) ||
      this.albumRemovals.size > 0 ||
      this.idRemovals.size > 0 ||
      this.additions.size > 0 ||
      this.deletions.size > 0
    );
  }

  /**
   * Removals, made whatever the action. An id on an album that's removed goes with the album, and
   * nothing is removed from a song to delete.
   */
  public get removals(): SharedIdRemovalRequest[] {
    const songs = this.cluster.songs.filter((s) => !this.isDeleted(s.songId));
    const albums = songs.flatMap((s) =>
      s.albums
        .filter((a) => this.isAlbumRemoved(s.songId, a.index))
        .map((a) => ({ songId: s.songId, album: a.index })),
    );
    const ids = songs.flatMap((s) =>
      s.albums
        .filter((a) => !this.isAlbumRemoved(s.songId, a.index))
        .flatMap((a) =>
          a.ids
            .filter((i) => this.idRemovals.has(idKey(s.songId, a.index, i.id)))
            .map((i) => ({ songId: s.songId, album: a.index, id: i.id })),
        ),
    );
    return [...albums, ...ids];
  }

  /** The cluster's entry in POST /api/sharedids/apply. */
  public toRequest(): SharedIdChoiceRequest {
    const merge = this.isMerge;
    const from = (field: NameField) => {
      const id = this.names[field];
      return merge && id !== null && this.canName(id) ? id : null;
    };
    // The text typed for a name, while it's the one picked
    const typed = (field: NameField) =>
      this.canType && this.names[field] === null ? this.typed[field] : null;
    const artists = typed("artists");
    const tempo = typed("tempo");
    return {
      action: this.action,
      candidateIds: this.cluster.candidates.map((c) => c.id),
      songs: this.cluster.songs.map((s) => ({ songId: s.songId, group: this.groupOf(s.songId) })),
      titleFrom: from("title"),
      artistFrom: from("artist"),
      artistsFrom: from("artists"),
      tempoFrom: from("tempo"),
      title: typed("title")?.trim() ?? null,
      artist: typed("artist")?.trim() ?? null,
      artists: artists === null ? null : parseArtists(artists),
      tempo: tempo === null ? null : parseTempo(tempo),
      removals: this.removals,
      additions: this.proposedAdditions.filter((a) => this.isAdded(a)),
      deletions: this.cluster.songs.map((s) => s.songId).filter((id) => this.isDeleted(id)),
    };
  }
}
