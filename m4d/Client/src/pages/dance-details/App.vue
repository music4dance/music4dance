<template>
  <page
    id="app"
    :breadcrumbs="breadcrumbs"
    :consumesEnvironment="true"
    @environment-loaded="onEnvironmentLoaded"
  >
    <b-row>
      <b-col
        ><h1>{{ model.danceName }}</h1></b-col
      >
      <b-col v-if="isAdmin" cols="auto">
        <b-button
          v-if="editing"
          variant="outline-primary"
          class="mr-1"
          @click="cancelChanges"
          >Cancel</b-button
        >
        <b-button
          v-if="editing"
          variant="primary"
          :disabled="!modified"
          @click="saveChanges"
          >Save</b-button
        >
        <b-button v-if="!editing" variant="primary" @click="startEdit"
          >Edit</b-button
        >
      </b-col>
    </b-row>
    <b-row>
      <b-col md="2" order-md="2">
        <dance-contents :model="model"></dance-contents>
      </b-col>
      <b-col md="10" order-md="1">
        <dance-description
          :description="model.description"
          :danceId="model.danceId"
          :editing="editing"
          @input="updateDescription($event)"
          ref="danceDescription"
        >
        </dance-description>
        <top-ten
          v-if="!isGroup"
          :histories="model.histories"
          :filter="model.filter"
          :userName="model.userName"
        ></top-ten>
        <spotify-player :playlist="model.spotifyPlaylist"></spotify-player>
        <dance-reference :danceId="model.danceId"></dance-reference>
        <div v-if="isGroup">
          <hr />
          <h2 id="dance-styles">
            Dances that are grouped into the {{ model.danceName }} category:
          </h2>
          <dance-list :group="this.dance" :showTempo="true"></dance-list>
        </div>
      </b-col>
    </b-row>
    <b-row id="competition-info" v-if="competitionInfo.length > 0">
      <b-col>
        <hr />
        <competition-category-table
          title="Competition Tempo Information"
          :dances="competitionInfo"
          :useFullName="true"
        ></competition-category-table>
      </b-col>
    </b-row>
    <b-row v-if="hasReferences">
      <b-col>
        <dance-links
          v-model="model.links"
          :editing="editing"
          :danceId="model.danceId"
          @update="updateLinks($event)"
          ref="danceLinks"
        ></dance-links>
      </b-col>
    </b-row>
    <b-row>
      <b-col>
        <hr />
        <h2 id="tags">Tags</h2>
        <tag-cloud
          :tags="tags"
          :user="userName"
          :songFilter="model.filter"
          :hideFilter="!showTagFilter"
        ></tag-cloud>
      </b-col>
    </b-row>
  </page>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Mixins } from "vue-property-decorator";
import AdminTools from "@/mix-ins/AdminTools";
import CompetitionCategoryTable from "@/components/CompetitionCategoryTable.vue";
import DanceContents from "./components/DanceContents.vue";
import DanceDescription from "./components/DanceDescription.vue";
import DanceList from "@/components/DanceList.vue";
import DanceLinks from "./components/DanceLinks.vue";
import DanceReference from "@/components/DanceReference.vue";
import Page from "@/components/Page.vue";
import SpotifyPlayer from "@/components/SpotifyPlayer.vue";
import TagCloud from "@/components/TagCloud.vue";
import TopTen from "./components/TopTen.vue";
import { DanceModel } from "@/model/DanceModel";
import { Editor } from "@/model/Editor";
import { SongFilter } from "@/model/SongFilter";
import { TypedJSON } from "typedjson";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { BreadCrumbItem, danceTrail } from "@/model/BreadCrumbItem";
import { DanceStats } from "@/model/DanceStats";
import { Tag } from "@/model/Tag";
import axios from "axios";
import { TypeStats } from "@/model/TypeStats";
import { DanceInstance } from "@/model/DanceInstance";
import { DanceLink } from "@/model/DanceLink";

declare const model: string;

//  Consider getting group to search for all of the songs in the group
@Component({
  components: {
    CompetitionCategoryTable,
    DanceContents,
    DanceDescription,
    DanceList,
    DanceLinks,
    DanceReference,
    Page,
    SpotifyPlayer,
    TagCloud,
    TopTen,
  },
})
export default class App extends Mixins(AdminTools) {
  private readonly model: DanceModel;
  private breadcrumbs: BreadCrumbItem[] = danceTrail;
  private tags: Tag[] = [];
  private isGroup = false;
  private dance: DanceStats | null = null;
  private editing = false;

  constructor() {
    super();

    this.model = TypedJSON.parse(model, DanceModel)!;
    this.model.filter = new SongFilter();
    this.model.filter.dances = this.model.danceId;
    this.model.filter.sortOrder = "Dances";
  }

  private onEnvironmentLoaded(environment: DanceEnvironment): void {
    const dance = environment.fromId(this.model.danceId);
    if (dance) {
      this.dance = dance;
      this.breadcrumbs = this.buildBreadCrumbs(dance);
      this.tags = dance.tags;
      this.isGroup = dance.isGroup;
    }
  }

  private buildBreadCrumbs(dance: DanceStats): BreadCrumbItem[] {
    return [...danceTrail, ...this.breadCrumbDetails(dance)];
  }

  private breadCrumbDetails(dance: DanceStats): BreadCrumbItem[] {
    return dance.isGroup
      ? [this.breadCrumbLeaf(dance)]
      : [this.breadCrumbGroup(dance as TypeStats), this.breadCrumbLeaf(dance)];
  }

  private breadCrumbLeaf(dance: DanceStats): BreadCrumbItem {
    return { text: dance.name, active: true };
  }

  private breadCrumbGroup(dance: TypeStats): BreadCrumbItem {
    const groupName = dance.groups![0].name;
    return { text: groupName, href: `/dances/${groupName}` };
  }

  private get showTagFilter(): boolean {
    return this.tags.length > 20;
  }

  private get competitionInfo(): DanceInstance[] {
    const dance = this.dance;
    return !dance || dance.isGroup
      ? []
      : (dance as TypeStats).competitionDances ?? [];
  }

  private get hasReferences(): boolean {
    return !!this.model.links && this.model.links.length > 0;
  }

  public get modified(): boolean {
    return this.descriptionEditor.isModified || this.linkEditor.isModified;
  }

  private get descriptionEditor(): Editor {
    return this.$refs.danceDescription as unknown as Editor;
  }

  private get linkEditor(): Editor {
    return this.$refs.links as unknown as Editor;
  }

  private updateDescription(value: string): void {
    this.model.description = value;
  }

  private updateLinks(value: DanceLink[]): void {
    this.model.links = value;
  }

  private startEdit(): void {
    this.editing = true;
  }

  private cancelChanges(): void {
    this.editing = false;
  }

  private async saveChanges(): Promise<void> {
    try {
      const model = this.model;
      await axios.patch(`/api/dances/${model.danceId}`, {
        id: model.danceId,
        description: model.description,
        danceLinks: model.links,
      });
      this.descriptionEditor.commit();
      this.linkEditor.commit();
      this.editing = false;
    } catch (e) {
      console.log(e);
      throw e;
    }
  }
}
</script>
