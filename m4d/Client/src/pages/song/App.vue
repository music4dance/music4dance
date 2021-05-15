<template>
  <page
    id="app"
    :consumesEnvironment="true"
    @environment-loaded="onEnvironmentLoaded"
  >
    <song-core :model="model" :environment="environment"></song-core>
  </page>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Mixins } from "vue-property-decorator";
import AdminTools from "@/mix-ins/AdminTools";
import Page from "@/components/Page.vue";
import SongCore from "./components/SongCore.vue";
import { TypedJSON } from "typedjson";
import { SongDetailsModel } from "@/model/SongDetailsModel";
import { DanceEnvironment } from "@/model/DanceEnvironmet";

declare const model: string;

// TODO:
//  Figure out how to be better about updating global taglist - this works, but we're doing
//   it every 6 hours which seems too inferquent - but it's an expensiveoperation, so
//   don't necessarily want to so it if not needed
//  Consider removing negating properties (this would help state changes)
//  Moderator remove dance: Uses UserProxy to removeTag for the dance
//   for each user, then reproxies to the moderator to negate the dance rating.
//   Consider just having a moderator event in the details that negates cancels out a dance.
//  Consider property history as a public facing feature
//  Make property history editable?

@Component({
  components: {
    Page,
    SongCore,
  },
})
export default class App extends Mixins(AdminTools) {
  private readonly model: SongDetailsModel;
  private environment: DanceEnvironment | null = null;

  constructor() {
    super();

    this.model = TypedJSON.parse(model, SongDetailsModel)!;
  }

  private onEnvironmentLoaded(environment: DanceEnvironment): void {
    this.environment = environment;
  }
}
</script>
