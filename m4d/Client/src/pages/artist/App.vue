<template>
  <page id="app" :title="title">
    <song-table
      :histories="model.histories"
      :filter="filter"
      :hideSort="true"
      :hiddenColumns="['artist', 'length', 'track']"
    ></song-table>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import SongTable from "@/components/SongTable.vue";
import { safeEnvironment } from "@/helpers/DanceEnvironmentManager";
import { ArtistModel } from "@/model/ArtistModel";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import { SongFilter } from "@/model/SongFilter";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import Vue from "vue";

declare const model: string;

export default Vue.extend({
  components: { Page, SongTable },
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
  },
});
</script>
