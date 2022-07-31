<template>
  <page id="app" title="New Music" :consumesEnvironment="true">
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
    ></song-table>
    <song-footer :model="model"></song-footer>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import SongFooter from "@/components/SongFooter.vue";
import SongTable from "@/components/SongTable.vue";
import { SongFilter } from "@/model/SongFilter";
import { SongListModel } from "@/model/SongListModel";
import { SortOrder } from "@/model/SongSort";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { Component, Vue } from "vue-property-decorator";

declare const model: string;

// TODONEXT: Implement comment sort order on back end (make front end "Comment" match back end "Comments")
// Reload songs-a
// Push these changes out to the server
@Component({
  components: {
    Page,
    SongFooter,
    SongTable,
  },
})
export default class App extends Vue {
  private readonly model: SongListModel;
  private readonly added: boolean;
  private readonly changed: boolean;
  private readonly commented: boolean;

  constructor() {
    super();

    this.model = TypedJSON.parse(model, SongListModel)!;
    this.added = this.model.filter.sortOrder === SortOrder.Created;
    this.changed = this.model.filter.sortOrder === SortOrder.Modified;
    this.commented = this.model.filter.sortOrder === SortOrder.Comments;
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

  private clickCommented(): void {
    this.navigate(SortOrder.Comments);
  }

  private navigate(order: SortOrder): void {
    window.location.href = `/song/newmusic?type=${order}`;
  }
}
</script>
