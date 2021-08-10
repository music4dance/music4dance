<template>
  <page id="app" :consumesEnvironment="true">
    <song-library-header
      v-if="model.filter.isSimple(model.userName)"
      :filter="model.filter"
      :user="model.userName"
    ></song-library-header>
    <search-header
      v-else
      :filter="model.filter"
      :user="model.userName"
    ></search-header>
    <song-table
      :histories="model.histories"
      :filter="filter"
      :hideSort="model.hideSort"
      :hiddenColumns="['length', 'track']"
      @song-selected="selectSong"
    ></song-table>
    <song-footer
      :model="model"
      :canShowImplicitMessage="true"
      :selected="selected"
    ></song-footer>
  </page>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Vue } from "vue-property-decorator";
import SearchHeader from "@/components/SearchHeader.vue";
import SongFooter from "@/components/SongFooter.vue";
import SongLibraryHeader from "@/components/SongLibraryHeader.vue";
import SongTable from "@/components/SongTable.vue";
import Page from "@/components/Page.vue";
import { TypedJSON } from "typedjson";
import { SongFilter } from "@/model/SongFilter";
import { SongListModel } from "@/model/SongListModel";

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
  private selected: string[] = [];

  constructor() {
    super();

    this.model = TypedJSON.parse(model, SongListModel)!;
  }

  private get filter(): SongFilter {
    return this.model.filter;
  }

  private selectSong(songId: string, selected: boolean): void {
    if (selected) {
      if (!this.selected.find((s) => s === songId)) {
        this.selected.push(songId);
      }
    } else {
      this.selected = this.selected.filter((s) => s !== songId);
    }
  }
}
</script>
