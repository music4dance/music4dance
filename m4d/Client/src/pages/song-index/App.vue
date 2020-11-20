<template>
  <page id="app" :consumesEnvironment="true">
    <song-library-header
      v-if="filter.isSimple(model.user)"
      :filter="filter"
      :user="model.user"
    ></song-library-header>
    <search-header v-else :filter="filter" :user="model.user"></search-header>
    <song-table
      :songs="songs"
      :filter="filter"
      :userName="model.userName"
      :hideSort="model.hideSort"
      :hiddenColumns="model.hiddenColumns"
    ></song-table>
    <song-footer :filter="filter" :count="model.count"></song-footer>
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
import { Song } from "@/model/Song";
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

  constructor() {
    super();

    this.model = TypedJSON.parse(model, SongListModel)!;
  }

  private get songs(): Song[] {
    return this.model.songs;
  }

  private get filter(): SongFilter {
    return this.model.filter;
  }
}
</script>
