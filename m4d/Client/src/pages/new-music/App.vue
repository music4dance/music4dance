<template>
  <page id="app" title="New Music" :consumesEnvironment="true">
    <b-button-group>
      <b-button variant="outline-primary" :pressed="added" @click="clickAdded">Recently Added</b-button>
      <b-button variant="outline-primary" :pressed="changed" @click="clickChanged">Recently Changed</b-button>
    </b-button-group>
    <song-table 
      :songs="songs"
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
import SearchHeader from '@/components/SearchHeader.vue';
import SongFooter from '@/components/SongFooter.vue';
import SongLibraryHeader from '@/components/SongLibraryHeader.vue';
import SongTable from '@/components/SongTable.vue';
import Page from '@/components/Page.vue';
import { jsonObject, TypedJSON, jsonArrayMember, jsonMember } from 'typedjson';
import { Song } from '@/model/Song';
import { SongFilter } from '@/model/SongFilter';
import { DanceStats } from '@/model/DanceStats';
import { SortOrder } from '@/model/SongSort';
import { SongListModel } from '@/model/SongListModel';

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
  private readonly added: boolean;
  private readonly changed: boolean;

  constructor() {
    super();

    this.model = TypedJSON.parse(model, SongListModel)!;
    this.added = this.model.filter.sortOrder === SortOrder.Created;
    this.changed = this.model.filter.sortOrder === SortOrder.Modified;
  }

  private get songs(): Song[] {
    return this.model.songs;
  }

  private get filter(): SongFilter {
    return this.model ? this.model.filter : new SongFilter();
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
}
</script>
