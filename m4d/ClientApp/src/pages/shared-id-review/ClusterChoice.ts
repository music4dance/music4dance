import type {
  SharedIdAction,
  SharedIdChoiceRequest,
  SharedIdRemovalRequest,
  SharedIdReviewCluster,
} from "./SharedIdReviewModel";

// The names a merged song takes, each from one of the cluster's songs or, as null, from the
// If merged column's proposal (the feat. cleanup of the merge)
export type NameField = "title" | "artist" | "artists";

export const nameFields: NameField[] = ["title", "artist", "artists"];

// "Leave out" in a song's group select
export const leaveOut = "";

/** The groups a cluster's songs can be merged in: one per song, A, B, … */
export function groupNames(songCount: number): string[] {
  return Array.from({ length: songCount }, (_, i) => String.fromCharCode(65 + i));
}

function albumKey(songId: string, album: number): string {
  return `${songId}|${album}`;
}

function idKey(songId: string, album: number, id: string): string {
  return `${songId}|${album}|${id}`;
}

/**
 * What the reviewer has chosen for one cluster of the shared-id review page. Starts from the
 * cluster's defaults: its default action, every song in group A, every name from the proposal
 * (or the first song when there's none) and nothing removed. The server re-checks everything on
 * Apply (SharedIdLog.Apply); this only collects the choice and says which controls apply.
 *
 * Wrap an instance in reactive() to use it in a component: its state is plain fields, a Map and
 * a Set, which Vue tracks.
 */
export class ClusterChoice {
  public action: SharedIdAction;
  public readonly groups = new Map<string, string>();
  public readonly names: Record<NameField, string | null>;
  public readonly albumRemovals = new Set<string>();
  public readonly idRemovals = new Set<string>();

  public constructor(public readonly cluster: SharedIdReviewCluster) {
    this.action = cluster.defaultAction;
    cluster.songs.forEach((s) => this.groups.set(s.songId, "A"));
    this.names = ClusterChoice.defaultNames(cluster);
  }

  private static defaultNames(cluster: SharedIdReviewCluster): Record<NameField, string | null> {
    const from = cluster.proposal ? null : (cluster.songs[0]?.songId ?? null);
    return { title: from, artist: from, artists: from };
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

  public groupOf(songId: string): string {
    return this.groups.get(songId) ?? leaveOut;
  }

  public setGroup(songId: string, group: string): void {
    this.groups.set(songId, group);
    // A song left out of every group can't name a merged song
    if (group === leaveOut) {
      nameFields
        .filter((f) => this.names[f] === songId)
        .forEach((f) => (this.names[f] = ClusterChoice.defaultNames(this.cluster)[f]));
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

  /** The choice differs from the cluster's defaults. */
  public get isDirty(): boolean {
    const defaults = ClusterChoice.defaultNames(this.cluster);
    return (
      this.action !== this.cluster.defaultAction ||
      this.cluster.songs.some((s) => this.groupOf(s.songId) !== "A") ||
      nameFields.some((f) => this.names[f] !== defaults[f]) ||
      this.albumRemovals.size > 0 ||
      this.idRemovals.size > 0
    );
  }

  /** Removals, made whatever the action. An id on an album that's removed goes with the album. */
  public get removals(): SharedIdRemovalRequest[] {
    const songs = this.cluster.songs;
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
    return {
      action: this.action,
      candidateIds: this.cluster.candidates.map((c) => c.id),
      songs: this.cluster.songs.map((s) => ({ songId: s.songId, group: this.groupOf(s.songId) })),
      titleFrom: from("title"),
      artistFrom: from("artist"),
      artistsFrom: from("artists"),
      removals: this.removals,
    };
  }
}
