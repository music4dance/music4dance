<template>
  <page id="app">
    <h1>
      Album: {{ model.title }}
      <span v-if="model.artist"
        >by <a :href="artistRef">{{ model.artist }}</a></span
      >
    </h1>
    <song-table
      :histories="model.histories"
      :filter="model.filter"
      :hideSort="true"
      :hiddenColumns="hidden"
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
import { AlbumModel } from "@/model/AlbumModel";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import SongSelector from "@/mix-ins/SongSelector";

declare const model: string;

export default SongSelector.extend({
  components: { AdminFooter, Page, SongTable },
  data() {
    return new (class {
      model: AlbumModel = TypedJSON.parse(model, AlbumModel)!;
      environment: DanceEnvironment = safeEnvironment();
    })();
  },
  computed: {
    artistRef(): string {
      return `/song/artist?name=${encodeURIComponent(this.model.artist || "")}`;
    },
    hidden(): string[] {
      return this.model.artist ? ["Artist"] : [];
    },
  },
});
</script>
