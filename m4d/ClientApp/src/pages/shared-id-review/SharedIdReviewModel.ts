import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

// Mirrors m4d/ViewModels/SharedIdReviewModel.cs. The server leaves nulls out, so anything that
// can be null there is optional here.

export type SharedIdAction = "Leave" | "Merge" | "Reject";
export type SharedIdMatch =
  "Exact" | "WithoutFeaturing" | "SameArtists" | "FewerArtists" | "Close" | "Different";

@jsonObject
export class SharedIdReviewTrackId {
  // As AlbumDetails.GetExtendedPurchaseIds spells it: "S:…", "I:…", "R:…"
  @jsonMember(String) public id!: string;
  // The service's page for the track; ISRCs have none
  @jsonMember(String) public link?: string;
  // One of the ids the cluster's songs share
  @jsonMember(Boolean) public shared!: boolean;
}

@jsonObject
export class SharedIdReviewAlbum {
  // AlbumDetails.Index: what a removal names
  @jsonMember(Number) public index!: number;
  @jsonMember(String) public name?: string;
  @jsonMember(Number) public track?: number;
  @jsonArrayMember(SharedIdReviewTrackId) public ids!: SharedIdReviewTrackId[];
}

@jsonObject
export class SharedIdReviewDance {
  @jsonMember(String) public danceId!: string;
  @jsonMember(Number) public weight!: number;
}

@jsonObject
export class SharedIdReviewSong {
  @jsonMember(String) public songId!: string;
  @jsonMember(String) public title?: string;
  @jsonMember(String) public artist?: string;
  // The individual artists (Song.EffectiveArtists)
  @jsonArrayMember(String) public artists!: string[];
  @jsonMember(Number) public length?: number;
  @jsonMember(Number) public tempo?: number;
  @jsonArrayMember(SharedIdReviewDance) public dances!: SharedIdReviewDance[];
  @jsonMember(String) public created!: string;
  @jsonArrayMember(SharedIdReviewAlbum) public albums!: SharedIdReviewAlbum[];
}

@jsonObject
export class SharedIdReviewNames {
  @jsonMember(String) public title?: string;
  @jsonMember(String) public artist?: string;
  @jsonArrayMember(String) public artists!: string[];
}

@jsonObject
export class SharedIdReviewCandidate {
  @jsonMember(Number) public id!: number;
  @jsonMember(String) public source?: string;
  @jsonMember(String) public created!: string;
  @jsonMember(String) public serviceIds?: string;
  @jsonMember(String) public detail?: string;
}

@jsonObject
export class SharedIdReviewCluster {
  // The cluster's song ids, sorted
  @jsonMember(String) public key!: string;
  @jsonArrayMember(String) public serviceIds!: string[];
  @jsonMember(String) public match!: SharedIdMatch;
  @jsonMember(Boolean) public hasRejectedPair!: boolean;
  @jsonMember(Boolean) public hasNoMergeSong!: boolean;
  @jsonMember(Boolean) public cleansFeaturing!: boolean;
  // The longest song minus the shortest, in seconds, and whether that's beyond the server's
  // tolerance (SharedIdMatcher.LengthTolerance)
  @jsonMember(Number) public lengthSpread?: number;
  @jsonMember(Boolean) public lengthsDiffer?: boolean;
  // The lengths are beyond the tolerance, but every song's tempo is within
  // SharedIdMatcher.TempoTolerance of the others, so they don't count
  @jsonMember(Boolean) public temposAgree?: boolean;
  @jsonMember(String) public defaultAction!: SharedIdAction;
  @jsonArrayMember(SharedIdReviewCandidate) public candidates!: SharedIdReviewCandidate[];
  // Empty when the songs changed while the page loaded
  @jsonArrayMember(SharedIdReviewSong) public songs!: SharedIdReviewSong[];
  // What merging every song would make; missing when the merge couldn't be previewed
  @jsonMember(SharedIdReviewSong) public merged?: SharedIdReviewSong;
  // The feat. cleanup of that merge
  @jsonMember(SharedIdReviewNames) public proposal?: SharedIdReviewNames;
}

@jsonObject
export class SharedIdReviewModel {
  @jsonMember(Number) public page!: number;
  @jsonMember(Number) public pageCount!: number;
  @jsonMember(Number) public pendingCandidates!: number;
  @jsonMember(Number) public clusterCount!: number;
  // When the page is sorted by the last merge-all preview: when it ran
  @jsonMember(String) public sortedBy?: string;
  @jsonMember(Number) public withoutFeaturingCount!: number;
  @jsonMember(Number) public closeCount!: number;
  // What that preview found the Merge all tasks would merge, as of when it ran
  @jsonMember(Number) public wouldMergeCount?: number;
  @jsonMember(Number) public wouldMergeFeaturingCount?: number;
  @jsonMember(Number) public wouldMergeCreditsCount?: number;
  // Clusters left for later, which come after the rest
  @jsonMember(Number) public deferredCount?: number;
  @jsonMember(String) public message?: string;
  @jsonArrayMember(SharedIdReviewCluster) public clusters!: SharedIdReviewCluster[];
}

// POST /api/sharedids/apply (SharedIdApplyRequest)
export interface SharedIdRemovalRequest {
  songId: string;
  album: number;
  id?: string;
}

// An id one song had to give another, its owner (SharedIdAddition): from the album at fromAlbum
// of fromSong, with the ISRC its track reports
export interface SharedIdAdditionRequest {
  songId: string;
  id: string;
  fromSong: string;
  fromAlbum: number;
  isrc?: string;
}

export interface SharedIdChoiceRequest {
  action: SharedIdAction;
  candidateIds: number[];
  songs: { songId: string; group: string }[];
  titleFrom: string | null;
  artistFrom: string | null;
  artistsFrom: string | null;
  tempoFrom: string | null;
  // What was typed over the proposal's names and tempo, or null for each left alone
  title: string | null;
  artist: string | null;
  artists: string[] | null;
  tempo: number | null;
  removals: SharedIdRemovalRequest[];
  additions: SharedIdAdditionRequest[];
  deletions: string[];
}

export interface SharedIdApplyRequest {
  page: number;
  choices: SharedIdChoiceRequest[];
}

// SharedIdApplyResponse
export interface SharedIdApplyResponse {
  message: string;
  merged: number;
  rejected: number;
  left: number;
  edited: number;
  unsharedPairs: number;
  stale: number;
  deleted: number;
}

// POST /api/sharedids/owners (SharedIdOwnersResponse): which of a cluster's songs each of their
// ids belongs to. Owned: song owns it, and it comes off the others (and goes to song when it
// doesn't have it); Bad: no song is the track, or the service doesn't have it, and it comes off
// every song; Unsure: nothing is proposed. Via is, for an ISRC, the id whose track reported it;
// shared is false for a song's own id.
export type SharedIdOwnerStatus = "Owned" | "Bad" | "Unsure";

export interface SharedIdOwnerProposal {
  id: string;
  status: SharedIdOwnerStatus;
  reason?: string;
  song?: string;
  via?: string;
  shared?: boolean;
}

export interface SharedIdOwnersResponse {
  ids: SharedIdOwnerProposal[];
  // The removals that carry the proposal out: an album, when they'd take every id off it
  removals: SharedIdRemovalRequest[];
  tracks: SharedIdServiceTrack[];
  // The ids that go to an owner that doesn't have them
  additions?: SharedIdAdditionRequest[];
}

// POST /api/sharedids/artists (SharedIdArtistsResponse): the cluster's songs' artists combined,
// as a credit joined with " & " and as the individual artists. Confirmed when the track via (a
// Spotify or iTunes id the songs have) credits all of them; otherwise missing is the ones it
// leaves out, or reason says why nothing was checked. Not checked at all for Force merge artist.
export interface SharedIdArtistsResponse {
  artist?: string;
  artists: string[];
  confirmed: boolean;
  via?: string;
  missing: string[];
  reason?: string;
}

// POST /api/sharedids/tracks: what a music service says one of the ids is (SharedIdServiceTrack)
export interface SharedIdServiceTrack {
  id: string;
  found: boolean;
  title?: string;
  artist?: string;
  album?: string;
  length?: number;
  isrc?: string;
  releaseDate?: string;
}
