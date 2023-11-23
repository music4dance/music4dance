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
      :hideSort="false"
      :hiddenColumns="hiddenColumns"
      @song-selected="selectSong"
    ></song-table>
    <song-footer :model="model"></song-footer>
    <admin-footer :model="model" :selected="selected"></admin-footer>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import SearchHeader from "@/components/SearchHeader.vue";
import AdminFooter from "@/components/AdminFooter.vue";
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
import mixins from "vue-typed-mixins";
import SongSelector from "@/mix-ins/SongSelector";

declare const model: string;

export default mixins(AdminTools, SongSelector).extend({
  components: {
    AdminFooter,
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
});
</script>
