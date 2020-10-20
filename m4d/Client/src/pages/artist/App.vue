<template>
  <page id="app" :title="title" :consumesEnvironment="true">
    <song-table 
      :songs="model.songs"
      :filter="model.filter"
      :userName="model.userName"
      :hideSort="true"
      :hiddenColumns="['Artist']"
    ></song-table>
  </page>
</template>

<script lang="ts">
// tslint:disable: max-classes-per-file
import 'reflect-metadata';
import { Component, Vue } from 'vue-property-decorator';
import SongTable from '@/components/SongTable.vue';
import Page from '@/components/Page.vue';
import { TypedJSON } from 'typedjson';
import { Song } from '@/model/Song';
import { ArtistModel } from '@/model/ArtistModel';

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
}
</script>
