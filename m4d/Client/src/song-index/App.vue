<template>
  <page id="app">
    <h1>Search Results</h1>
    <h3>
      {{ model.filter.description }}
      <b-button :href="changeLink">Change</b-button>
    </h3>
    <song-table 
      :v-if="loaded"
      :songs="songs"
      :environment="environment"
      :filter="model.filter"
      :userName="model.userName"
      :hideSort="model.hideSort"
      :hiddenColumns="model.hiddenColumns"
    ></song-table>
    <song-footer
      :filter="model.filter"
      :count="model.count"
    ></song-footer>
  </page>
</template>

<script lang="ts">
// tslint:disable: max-classes-per-file
import 'reflect-metadata';
import { Component, Vue } from 'vue-property-decorator';
import SongFooter from '../components/SongFooter.vue';
import SongTable from '../components/SongTable.vue';
import Page from '../components/Page.vue';
import { jsonObject, TypedJSON, jsonArrayMember, jsonMember } from 'typedjson';
import { Song } from '@/model/Song';
import { SongFilter } from '@/model/SongFilter';
import { DanceStats } from '@/model/DanceStats';
import { getEnvironment } from '@/helpers/DanceEnvironmentManager';
import { DanceEnvironment } from '@/model/DanceEnvironmet';

@jsonObject class SongListModel {
    @jsonMember public filter!: SongFilter;
    @jsonArrayMember(Song) public songs!: Song[];
    @jsonMember public userName!: string;
    @jsonMember public count!: number;
    @jsonMember public hideSort!: boolean;
    @jsonArrayMember(String) public hiddenColumns!: string[];
}

declare const model: string;

@Component({
  components: {
    Page,
    SongFooter,
    SongTable,
  },
})
export default class App extends Vue {
  private readonly model: SongListModel;
  private environment: DanceEnvironment = new DanceEnvironment();

  constructor() {
    super();

    TypedJSON.setGlobalConfig({
        errorHandler: (e) => {
            // tslint:disable-next-line:no-console
            console.error(e);
            throw e;
        },
    });

    this.model = TypedJSON.parse(model, SongListModel)!;

    // tslint:disable-next-line:no-console
    console.log(this.model.songs.length);
  }

  private get loaded(): boolean {
    const stats = this.environment?.stats;
    return !!stats && stats.length > 0;
  }

  private get songs(): Song[] {
    return this.loaded ? this.model.songs : [];
  }

  private get filter(): SongFilter {
    return this.loaded ? this.model.filter : new SongFilter();
  }

  private get changeLink(): string {
    return `/song/advancedsearchform?filter=${this.filter.encodedQuery}`;
  }

  private async created() {
    this.environment = await getEnvironment();

    // tslint:disable-next-line:no-console
    console.log(this.environment!.stats!.length);
  }
}
</script>
