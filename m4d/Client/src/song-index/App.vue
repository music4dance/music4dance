<template>
  <page id="app">
    <h1>Search Results</h1>
    <h2>{{ model.filter.description }}</h2>
    <song-table 
      :songs="model.songs"
      :environment="environment"
      :filter="model.filter"
      :userName="model.userName"
    ></song-table>
  </page>
</template>

<script lang="ts">
// tslint:disable: max-classes-per-file
import 'reflect-metadata';
import { Component, Vue } from 'vue-property-decorator';
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
}

declare const model: string;

@Component({
  components: {
    Page,
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

  private async created() {
    this.environment = await getEnvironment();

    // tslint:disable-next-line:no-console
    console.log(this.environment!.stats!.length);
  }
}
</script>
