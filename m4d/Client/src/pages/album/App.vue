<template>
  <page id="app" :consumesEnvironment="true">
    <h1>
      Album: {{ model.title }}
      <span v-if="model.artist"
        >by <a :href="artistRef">{{ model.artist }}</a></span
      >
    </h1>
    <song-table
      :songs="model.songs"
      :filter="model.filter"
      :userName="model.userName"
      :hideSort="true"
      :hiddenColumns="hidden"
    ></song-table>
  </page>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Vue } from "vue-property-decorator";
import SongTable from "@/components/SongTable.vue";
import Page from "@/components/Page.vue";
import { TypedJSON } from "typedjson";
import { AlbumModel } from "@/model/AlbumModel";

declare const model: string;

@Component({
  components: {
    Page,
    SongTable,
  },
})
export default class App extends Vue {
  private readonly model: AlbumModel;

  constructor() {
    super();

    this.model = TypedJSON.parse(model, AlbumModel)!;
  }

  private get artistRef(): string {
    return `/song/artist?name=${this.model.artist}`;
  }

  private get hidden(): string[] {
    return this.model.artist ? ["Artist"] : [];
  }
}
</script>
