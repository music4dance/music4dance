<template>
  <page id="app" :consumesEnvironment="true">
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
    ></song-table>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import SongTable from "@/components/SongTable.vue";
import { AlbumModel } from "@/model/AlbumModel";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { Component, Vue } from "vue-property-decorator";

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
