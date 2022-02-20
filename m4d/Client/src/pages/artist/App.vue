<template>
  <page id="app" :title="title" :consumesEnvironment="true">
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
import { ArtistModel } from "@/model/ArtistModel";
import { SongFilter } from "@/model/SongFilter";
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
  private readonly model: ArtistModel;

  constructor() {
    super();

    this.model = TypedJSON.parse(model, ArtistModel)!;
  }

  private get title(): string {
    return `Artist: ${this.model.artist}`;
  }

  private get filter(): SongFilter {
    return new SongFilter();
  }
}
</script>
