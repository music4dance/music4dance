<script setup lang="ts">
import { ArtistModel } from "@/models/ArtistModel";
import { SongFilter } from "@/models/SongFilter";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { TypedJSON } from "typedjson";
import { Song } from "@/models/Song";
import { useSongSelector } from "@/composables/useSongSelector";

declare const model_: string;

const model = TypedJSON.parse(model_, ArtistModel)!;

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
const dances = danceIds.map((id) => danceDB.dances.find((d) => d.id === id)!);
</script>

<template>
  <PageFrame id="app" :title="title">
    <p>
      {{ histories.length }} {{ model.artist }} songs with suggestions for dances to dance to them:
      <span v-for="(dance, idx) in dances" :key="dance.id"
        ><span v-if="idx + 1 === dances.length && dances.length > 1"> and </span
        ><a :href="'/dances/' + dance.seoName">{{ dance.name }}</a
        ><span v-if="idx + 1 < dances.length && dances.length > 2">, </span></span
      >
    </p>
    <SongTable
      :histories="histories"
      :filter="filter"
      :hide-sort="true"
      :hidden-columns="['artist', 'length', 'track']"
      @song-selected="selectSong"
    ></SongTable>
    <AdminFooter :model="model" :selected="selected"></AdminFooter>
  </PageFrame>
</template>
