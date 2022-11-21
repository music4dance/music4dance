<template>
  <page
    id="app"
    :consumesEnvironment="true"
    :consumesTags="true"
    @environment-loaded="onEnvironmentLoaded"
  >
    <song-core
      :model="songDetails"
      :environment="environment"
      :startEditing="true"
      :creating="true"
    ></song-core>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import AdminTools from "@/mix-ins/AdminTools";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import { SongDetailsModel } from "@/model/SongDetailsModel";
import { SongFilter } from "@/model/SongFilter";
import { SongHistory } from "@/model/SongHistory";
import { SongMergeModel } from "@/model/SongMergeModel";
import SongCore from "@/pages/song/components/SongCore.vue";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { Component, Mixins } from "vue-property-decorator";

declare const model: string;

@Component({
  components: {
    Page,
    SongCore,
  },
})
export default class App extends Mixins(AdminTools) {
  private readonly model: SongMergeModel;
  private environment: DanceEnvironment | null = null;

  constructor() {
    super();

    this.model = TypedJSON.parse(model, SongMergeModel)!;
  }

  private get songDetails(): SongDetailsModel {
    return new SongDetailsModel({
      created: true,
      songHistory: SongHistory.merge(
        this.model.songId,
        this.model.songs,
        this.userName
      ),
      filter: new SongFilter(),
      userName: this.userName,
    });
  }

  private onEnvironmentLoaded(environment: DanceEnvironment): void {
    this.environment = environment;
  }
}
</script>
