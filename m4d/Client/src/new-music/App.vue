<template>
  <page id="app">
    <h1>New Music</h1>
    <b-button-group>
      <b-button variant="outline-primary" :pressed="added" @click="clickAdded">Recently Added</b-button>
      <b-button variant="outline-primary" :pressed="changed" @click="clickChanged">Recently Changed</b-button>
    </b-button-group>
    <song-table 
      :v-if="loaded"
      :songs="songs"
      :environment="environment"
      :filter="filter"
      :userName="model.userName"
      :hideSort="model.hideSort"
      :hiddenColumns="model.hiddenColumns"
    ></song-table>
    <song-footer
      :filter="filter"
      :count="model.count"
    ></song-footer>
  </page>
</template>

<script lang="ts">
// tslint:disable: max-classes-per-file
import 'reflect-metadata';
import { Component, Vue } from 'vue-property-decorator';
import SearchHeader from '../components/SearchHeader.vue';
import SongFooter from '../components/SongFooter.vue';
import SongLibraryHeader from '../components/SongLibraryHeader.vue';
import SongTable from '../components/SongTable.vue';
import Page from '../components/Page.vue';
import { jsonObject, TypedJSON, jsonArrayMember, jsonMember } from 'typedjson';
import { Song } from '@/model/Song';
import { SongFilter } from '@/model/SongFilter';
import { DanceStats } from '@/model/DanceStats';
import { getEnvironment } from '@/helpers/DanceEnvironmentManager';
import { DanceEnvironment } from '@/model/DanceEnvironmet';
import { SortOrder } from '@/model/SongSort';

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
    SearchHeader,
    SongFooter,
    SongLibraryHeader,
    SongTable,
  },
})
export default class App extends Vue {
  private readonly model: SongListModel;
  private environment: DanceEnvironment = new DanceEnvironment();
  private readonly added: boolean;
  private readonly changed: boolean;

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
    this.added = this.model.filter.sortOrder === SortOrder.Created;
    this.changed = this.model.filter.sortOrder === SortOrder.Modified;
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

  private clickAdded(): void {
    this.navigate(SortOrder.Created);
  }

  private clickChanged(): void {
    this.navigate(SortOrder.Modified);
  }

  private navigate(order: SortOrder): void {
    window.location.href = `/song/newmusic?type=${order}`;
  }

  private async created() {
    this.environment = await getEnvironment();

    // tslint:disable-next-line:no-console
    // console.log(this.environment!.stats!.length);
  }
}
</script>
