<template>
  <page id="app" title="New Music">
    <b-button-group>
      <b-button variant="outline-primary" :pressed="added" @click="clickAdded"
        >Recently Added</b-button
      >
      <b-button
        variant="outline-primary"
        :pressed="changed"
        @click="clickChanged"
        >Recently Changed</b-button
      >
      <b-button
        variant="outline-primary"
        :pressed="commented"
        @click="clickCommented"
        >Recently Commented</b-button
      >
    </b-button-group>
    <song-table
      :histories="model.histories"
      :filter="filter"
      :hideSort="true"
      :hiddenColumns="['length', 'track']"
      :showHistory="true"
      @song-selected="selectSong"
    ></song-table>
    <song-footer :model="model"></song-footer>
    <admin-footer :model="model" :selected="selected"></admin-footer>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import AdminFooter from "@/components/AdminFooter.vue";

import SongFooter from "@/components/SongFooter.vue";
import SongTable from "@/components/SongTable.vue";
import { safeEnvironment } from "@/helpers/DanceEnvironmentManager";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import { SongFilter } from "@/model/SongFilter";
import { SongListModel } from "@/model/SongListModel";
import { SortOrder } from "@/model/SongSort";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import SongSelector from "@/mix-ins/SongSelector";

declare const model: string;

export default SongSelector.extend({
  components: {
    AdminFooter,
    Page,
    SongFooter,
    SongTable,
  },
  props: {},
  data() {
    const m = TypedJSON.parse(model, SongListModel)!;
    return new (class {
      model: SongListModel = m;
      added: boolean = m.filter.sortOrder === SortOrder.Created;
      changed: boolean = m.filter.sortOrder === SortOrder.Modified;
      commented: boolean = m.filter.sortOrder === SortOrder.Comments;
      environment: DanceEnvironment = safeEnvironment();
    })();
  },
  computed: {
    filter(): SongFilter {
      return this.model ? this.model.filter : new SongFilter();
    },
  },
  methods: {
    clickAdded(): void {
      this.navigate(SortOrder.Created);
    },
    clickChanged(): void {
      this.navigate(SortOrder.Modified);
    },
    clickCommented(): void {
      this.navigate(SortOrder.Comments);
    },
    navigate(order: SortOrder): void {
      window.location.href = `/song/newmusic?type=${order}`;
    },
  },
});
</script>
