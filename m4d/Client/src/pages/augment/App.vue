<template>
  <page
    id="app"
    :consumesEnvironment="true"
    @environment-loaded="onEnvironmentLoaded"
  >
    <b-row v-if="phase === 'lookup'">
      <b-col>
        <b-alert v-if="lastSong" dismissible show="10">
          Thank you for {{ created ? "adding" : "editing" }}
          <i>{{ lastSong.title }}</i> by {{ lastSong.artist }}
        </b-alert>
        <h1>Add Song</h1>
        <b-tabs v-if="canAugment" v-model="tabIndex" card>
          <b-tab title="by Title"
            ><b-card-text
              ><augment-search
                @edit-song="editSong($event)"
              ></augment-search></b-card-text
          ></b-tab>
          <b-tab title="by Id">
            <b-card-text
              ><augment-lookup @edit-song="editSong($event)"></augment-lookup
            ></b-card-text>
          </b-tab>
          <b-tab v-if="isAdmin" title="Admin">
            <b-card-text>
              <p>Paste in text version of propery list</p>
              <b-input-group prepend="Properties">
                <b-form-input
                  id="admin-properties"
                  palceholder="Song Properties TSV"
                  v-model="propertiesString"
                  trim
                ></b-form-input>
                <b-button variant="primary" @click="adminCreate"
                  >Create</b-button
                >
              </b-input-group>
            </b-card-text>
          </b-tab>
        </b-tabs>
        <augment-info v-else :openAugment="openAugment"> </augment-info>
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
    <b-row
      ><b-col>
        <b-alert show variant="warning">
          Adding songs is a new feature so if you run into bugs or have
          suggestions for improving this feature please don't hesitate to send
          email to
          <a href="mailto:info@music4dance.net">info@music4dance.net</a> or fill
          out our
          <a href="https://music4dance.blog/feedback/">feedback form</a> and
          we'll be happy to take your input as we contineu to improve the
          feature.
        </b-alert>
      </b-col>
    </b-row>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import AdminTools from "@/mix-ins/AdminTools";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { Song } from "@/model/Song";
import { SongDetailsModel } from "@/model/SongDetailsModel";
import { SongFilter } from "@/model/SongFilter";
import { SongHistory } from "@/model/SongHistory";
import SongCore from "@/pages/song/components/SongCore.vue";
import "reflect-metadata";
import { Component, Mixins } from "vue-property-decorator";
import AugmentInfo from "./components/AugmentInfo.vue";
import AugmentLookup from "./components/AugmentLookup.vue";
import AugmentSearch from "./components/AugmentSearch.vue";

enum AugmentPhase {
  lookup = "lookup",
  results = "results",
  edit = "edit",
}

declare const openAugment: boolean;

@Component({
  components: { AugmentInfo, AugmentLookup, AugmentSearch, SongCore, Page },
})
export default class App extends Mixins(AdminTools) {
  private phase = AugmentPhase.lookup;
  private songModel: SongDetailsModel | null = null;
  private lastSong: Song | null = null;
  private created = false;
  private environment: DanceEnvironment | null = null;
  private propertiesString = "";
  private tabIndex = 0;

  private get canAugment(): boolean {
    return openAugment ? !!this.userName : this.canTag || this.isPremium;
  }

  private get openAugment(): boolean {
    return openAugment;
  }

  private editSong(model: SongDetailsModel): void {
    this.songModel = new SongDetailsModel({
      created: !!model.created,
      songHistory: model.songHistory,
      filter: new SongFilter(),
      userName: this.userName,
    });
    this.phase = AugmentPhase.edit;
  }

  private async adminCreate(): Promise<void> {
    this.songModel = new SongDetailsModel({
      created: true,
      songHistory: SongHistory.fromString(this.propertiesString),
      filter: new SongFilter(),
      userName: this.userName,
    });
    this.propertiesString = "";
    this.phase = AugmentPhase.edit;
  }

  private reset(saved: boolean): void {
    if (saved) {
      this.lastSong = Song.fromHistory(this.songModel!.songHistory);
      this.created = !!this.songModel!.created;
    }
    this.phase = AugmentPhase.lookup;
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
