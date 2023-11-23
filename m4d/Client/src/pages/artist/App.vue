<template>
  <page id="app" :title="title">
    <p>
      {{ histories.length }} {{ model.artist }} songs with suggestions for
      dances to dance to them:
      <span v-for="(dance, idx) in dances" :key="dance.id"
        ><span v-if="idx + 1 === dances.length && dances.length > 1"> and </span
        ><a :href="'/dances/' + dance.seoName">{{ dance.name }}</a
        ><span v-if="idx + 1 < dances.length && dances.length > 2"
          >,
        </span></span
      >
    </p>
    <song-table
      :histories="model.histories"
      :filter="filter"
      :hideSort="true"
      :hiddenColumns="['artist', 'length', 'track']"
      @song-selected="selectSong"
    ></song-table>
    <admin-footer :model="model" :selected="selected"></admin-footer>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import SongTable from "@/components/SongTable.vue";
import AdminFooter from "@/components/AdminFooter.vue";
import { safeEnvironment } from "@/helpers/DanceEnvironmentManager";
import { ArtistModel } from "@/model/ArtistModel";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import { SongFilter } from "@/model/SongFilter";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import SongSelector from "@/mix-ins/SongSelector";
import { Song } from "@/model/Song";
import { TypeStats } from "@/model/TypeStats";
import { SongHistory } from "@/model/SongHistory";

declare const model: string;

export default SongSelector.extend({
  components: { AdminFooter, Page, SongTable },
  props: {},
  data() {
    return new (class {
      model: ArtistModel = TypedJSON.parse(model, ArtistModel)!;
      environment: DanceEnvironment = safeEnvironment();
    })();
  },
  computed: {
    title(): string {
      return `Artist: ${this.model.artist}`;
    },
    filter(): SongFilter {
      return new SongFilter();
    },
    dances(): TypeStats[] {
      return this.danceIds.map(
        (id) => this.environment.dances!.find((d) => d.id === id)!
      );
    },
    danceIds(): string[] {
      return [
        ...this.songs.reduce((acc, sng) => {
          const ratings = sng.danceRatings;
          if (ratings) {
            ratings.forEach((r) => acc.add(r.danceId));
          }
          return acc;
        }, new Set<string>()),
      ];
    },
    songs(): Song[] {
      const histories = this.model.histories;
      return histories ? histories.map((h) => Song.fromHistory(h)) : [];
    },
    histories(): SongHistory[] {
      const histories = this.model.histories;
      return histories ?? [];
    },
  },
});
</script>
