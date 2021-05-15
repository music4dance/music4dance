<template>
  <page
    id="app"
    :consumesEnvironment="true"
    @environment-loaded="onEnvironmentLoaded"
  >
    <b-row v-if="lookup">
      <b-col>
        <b-alert v-if="lastSong" dismissible show="10">
          Thank you for {{ created ? "adding" : "editing" }}
          <i>{{ lastSong.title }}</i> by {{ lastSong.artist }}
        </b-alert>
        <h1>Add Song</h1>
        <augment-lookup
          v-if="canAugment"
          @edit-song="editSong($event)"
        ></augment-lookup>
        <augment-info v-else> </augment-info>
      </b-col>
    </b-row>
    <div v-else>
      <b-alert show variant="success">
        <div v-if="songModel.created">
          <b>Create Song:</b> This song is new to music4dance, please fill in
          missing fields and click <b>Add Song</b> to add this song to the
          catalog. Remember, you must vote on at least one dance to add this
          song.
        </div>
        <div v-else>
          <b>Edit Song:</b> We found this song in the music4dance catalog,
          please vote on dance styles and add tags to improve the catalog.
        </div>
      </b-alert>
      <song-core
        :model="songModel"
        :environment="environment"
        :startEditing="true"
        :creating="songModel.created"
        @song-saved="reset(true)"
        @cancel-changes="reset(false)"
      ></song-core>
    </div>
  </page>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Mixins } from "vue-property-decorator";
import AdminTools from "@/mix-ins/AdminTools";
import AugmentInfo from "./components/AugmentInfo.vue";
import AugmentLookup from "./components/AugmentLookup.vue";
import SongCore from "@/pages/song/components/SongCore.vue";
import Page from "@/components/Page.vue";
import { SongDetailsModel } from "@/model/SongDetailsModel";
import { SongFilter } from "@/model/SongFilter";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { Song } from "@/model/Song";

@Component({
  components: { AugmentInfo, AugmentLookup, SongCore, Page },
})
export default class App extends Mixins(AdminTools) {
  private lookup = true;
  private songModel: SongDetailsModel | null = null;
  private lastSong: Song | null = null;
  private created = false;
  private environment: DanceEnvironment | null = null;

  private get canAugment(): boolean {
    return this.canTag || this.isPremium;
  }

  private editSong(model: SongDetailsModel): void {
    this.songModel = new SongDetailsModel({
      created: !!model.created,
      songHistory: model.songHistory,
      filter: new SongFilter(),
      userName: this.userName,
    });
    this.lookup = false;
  }

  private reset(saved: boolean): void {
    if (saved) {
      this.lastSong = Song.fromHistory(this.songModel!.songHistory);
      this.created = !!this.songModel!.created;
    }
    this.lookup = true;
    this.songModel = null;
  }

  private onEnvironmentLoaded(environment: DanceEnvironment): void {
    this.environment = environment;
  }
}
</script>

<style lang="scss" scoped>
#service-id {
  width: 25rem;
}
</style>
