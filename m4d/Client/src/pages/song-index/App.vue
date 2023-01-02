<template>
  <page id="app">
    <b-alert
      v-if="complexSearchWarning"
      show
      dismissible
      variant="warning"
      style="margin-bottom: 0"
    >
      This is a complex search that requries multiple passes to compute. We're
      limitting the initial pass to 500 songs, which may result in much less
      that 500 songs in the final results as well as incomplete results (the
      intial pass in this cases yields {{ model.rawCount }} songs). We believe
      we can solve this is a more general way, so please
      <a href="https://music4dance.blog/feedback/" target="_blank"
        >send feedback</a
      >
      about what you are trying to accomplish with this search and we can either
      help you with an alternate search or increase the priority of building
      amore general solution.
    </b-alert>
    <song-library-header
      v-if="model.filter.isSimple(userName)"
      :filter="model.filter"
      :user="userName"
    ></song-library-header>
    <search-header
      v-else
      :filter="model.filter"
      :user="userName"
    ></search-header>
    <song-table
      :histories="model.histories"
      :filter="filter"
      :hideSort="model.hideSort"
      :hiddenColumns="hiddenColumns"
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
import Page from "@/components/Page.vue";
import SearchHeader from "@/components/SearchHeader.vue";
import SongFooter from "@/components/SongFooter.vue";
import SongLibraryHeader from "@/components/SongLibraryHeader.vue";
import SongTable from "@/components/SongTable.vue";
import { safeEnvironment } from "@/helpers/DanceEnvironmentManager";
import AdminTools from "@/mix-ins/AdminTools";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import { SongFilter } from "@/model/SongFilter";
import { SongListModel } from "@/model/SongListModel";
import "reflect-metadata";
import { TypedJSON } from "typedjson";

declare const model: string;

export default AdminTools.extend({
  components: {
    Page,
    SearchHeader,
    SongFooter,
    SongLibraryHeader,
    SongTable,
  },
  props: {},
  data() {
    return new (class {
      model: SongListModel = TypedJSON.parse(model, SongListModel)!;
      selected: string[] = [];
      environment: DanceEnvironment = safeEnvironment();
    })();
  },
  computed: {
    filter(): SongFilter {
      return this.model.filter;
    },
    complexSearchWarning(): boolean {
      const model = this.model;
      return model.rawCount > model.count && model.rawCount > 500;
    },
    hiddenColumns(): string[] {
      const columns = this.model.hiddenColumns;
      return columns ? columns : ["length", "track"];
    },
  },
  methods: {
    selectSong(songId: string, selected: boolean): void {
      if (selected) {
        if (!this.selected.find((s) => s === songId)) {
          this.selected.push(songId);
        }
      } else {
        this.selected = this.selected.filter((s) => s !== songId);
      }
    },
  },
});
</script>
