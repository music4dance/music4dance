<script setup lang="ts">
import { ArtistModel } from "@/models/ArtistModel";
import { SongFilter } from "@/models/SongFilter";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { TypedJSON } from "typedjson";
import { Song } from "@/models/Song";
import { useSongSelector } from "@/composables/useSongSelector";
import type { DanceType } from "@/models/DanceDatabase/DanceType";
import { KeywordQuery } from "@/models/KeywordQuery";
import { SongSort, SortOrder } from "@/models/SongSort";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { artistKey, artistPageUrl, collaborators } from "@/models/ArtistNames";

declare const model_: string;

const model = TypedJSON.parse(model_, ArtistModel)!;

const context = getMenuContext();
const danceDB = safeDanceDatabase();
const { songs: selected, select: selectSong } = useSongSelector();

const title = `Artist: ${model.artist}`;
const filter = new SongFilter();
const histories = model.histories ?? [];
const songs = histories.map((h) => Song.fromHistory(h));
const danceIds = [
  ...songs.reduce((acc, h) => {
    const ratings = h.danceRatings;
    if (ratings) {
      ratings.forEach((r) => acc.add(r.danceId));
    }
    return acc;
  }, new Set<string>()),
];

const invalidDanceIds: string[] = danceIds.filter((id) => !danceDB.dances.some((d) => d.id === id));
console.warn(
  `Invalid dance IDs found in artist model ${model.artist}: ${invalidDanceIds.join(", ")}`,
);
const dances = danceIds
  .filter((id) => !invalidDanceIds.includes(id))
  .map((id) => danceDB.dances.find((d) => d.id === id)!);

const danceFilter = (dance: DanceType) => {
  const f = new SongFilter();
  f.searchString = KeywordQuery.fromParts(new Map([["Artist", model.artist]])).query;
  f.dances = dance.id;
  f.sortOrder = SongSort.fromParts(SortOrder.Dances).query;
  return f;
};

const artistSearchFilter = () => {
  const f = new SongFilter();
  f.searchString = KeywordQuery.fromParts(new Map([["Artist", model.artist]])).query;
  return f;
};

// With individual artists, the page includes collaborations credited differently, so the artist
// column is only redundant when every credit is exactly this artist
const individualArtists = !!context.artistIndex;
const hiddenColumns = [
  ...(individualArtists && songs.some((s) => artistKey(s.artist) !== artistKey(model.artist))
    ? []
    : ["artist"]),
  "danceTags",
  "length",
  "track",
];
const appearsWith = individualArtists ? collaborators(model.artist, songs).slice(0, 20) : [];

const danceCount = (dance: DanceType) => {
  return songs.filter((s) => s.danceRatings?.some((r) => r.danceId === dance.id)).length;
};
</script>

<template>
  <PageFrame id="app" :title="title">
    <p>
      {{ histories.length }} {{ model.artist }} songs with suggestions for dances to dance to them:
      <span v-for="(dance, idx) in dances" :key="dance.id"
        ><span v-if="idx + 1 === dances.length && dances.length > 1"> and </span
        ><a :href="'/dances/' + dance.seoName">{{ dance.name }}&nbsp;</a
        ><a :href="'/song/filtersearch?filter=' + danceFilter(dance).query"
          >( {{ danceCount(dance) }} song{{ danceCount(dance) > 1 ? "s" : "" }} )</a
        ><span v-if="idx + 1 < dances.length && dances.length > 2">, </span></span
      >
    </p>
    <p v-if="appearsWith.length">
      Also appears with:
      <span v-for="(other, idx) in appearsWith" :key="other.artist"
        ><span v-if="idx > 0">, </span><a :href="artistPageUrl(other.artist)">{{ other.artist }}</a>
        ({{ other.count }})</span
      >
    </p>
    <p>
      <a :href="'/song/filtersearch?filter=' + artistSearchFilter().query">
        Search all songs with "{{ model.artist }}" in the artist field
      </a>
      <template v-if="individualArtists">
        &middot; <a href="/song/artists">Browse all artists</a>
      </template>
    </p>
    <SongTable
      :histories="histories"
      :filter="filter"
      :hide-sort="true"
      :hidden-columns="hiddenColumns"
      @song-selected="selectSong"
    />
    <AdminFooter :model="model" :selected="selected" />
  </PageFrame>
</template>
