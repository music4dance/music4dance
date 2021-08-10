<template>
  <page
    id="app"
    :consumesEnvironment="true"
    :breadcrumbs="breadcrumbs"
    @environment-loaded="onEnvironmentLoaded"
  >
    <h1>{{ title }}</h1>
    <holiday-help v-if="model.count === 0" :dance="model.dance" :empty="true">
    </holiday-help>
    <div v-else>
      <p v-if="model.dance">
        This page includes a list of all of the
        <a :href="danceLink">{{ danceName }}</a> music on the site that has been
        tagged as "Holiday" or "Christmas". Click
        <a href="/song/holidaymusic">here</a> to see holiday music for all dance
        styles.
      </p>
      <p v-else>
        This page includes a list of all of the
        <a href="/dances/ballroom-competition-categories">Ballroom</a> and other
        <a href="/dances">partner dance</a> music on the site that has been
        tagged as "Holiday" or "Christmas".
      </p>
      <song-table
        :histories="model.histories"
        :filter="model.filter"
        :hideSort="model.hideSort"
        :hiddenColumns="['Track']"
      ></song-table>
      <song-footer :model="model" :href="pageLink"></song-footer>
    </div>
    <spotify-player :playlist="model.playListId"></spotify-player>
    <holiday-dance-chooser
      :dance="model.dance"
      :count="model.count"
    ></holiday-dance-chooser>
  </page>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Vue } from "vue-property-decorator";
import HolidayDanceChooser from "./HolidayDanceChooser.vue";
import HolidayHelp from "./HolidayHelp.vue";
import SongFooter from "@/components/SongFooter.vue";
import SongTable from "@/components/SongTable.vue";
import SpotifyPlayer from "@/components/SpotifyPlayer.vue";
import Page from "@/components/Page.vue";
import { TypedJSON } from "typedjson";
import { HolidaySongListModel } from "@/model/HolidaySongListModel";
import { toTitleCase, wordsToKebab } from "@/helpers/StringHelpers";
import { BreadCrumbItem, homeCrumb, songCrumb } from "@/model/BreadCrumbItem";

declare const model: string;

@Component({
  components: {
    HolidayDanceChooser,
    HolidayHelp,
    Page,
    SongFooter,
    SongTable,
    SpotifyPlayer,
  },
})
export default class App extends Vue {
  private readonly model: HolidaySongListModel;

  constructor() {
    super();

    this.model = TypedJSON.parse(model, HolidaySongListModel)!;
    console.log("Model loaded");
  }

  private get title(): string {
    return this.model.dance
      ? `Holiday ${toTitleCase(this.model.dance)} Music`
      : "Holiday Dance Music";
  }

  private get pageLink(): string {
    const dance = this.model.dance;
    return dance ? `/song/holidaymusic?dance=${dance}` : "/song/holidaymusic";
  }

  private get danceLink(): string {
    return `/dances/${this.seoDanceName}`;
  }

  private get danceName(): string | undefined {
    const dance = this.model.dance;
    return dance ? toTitleCase(dance) : undefined;
  }

  private get seoDanceName(): string | undefined {
    const dance = this.model.dance;
    return dance ? wordsToKebab(dance) : undefined;
  }

  private get breadcrumbs(): BreadCrumbItem[] {
    const breadcrumbs = [homeCrumb, songCrumb];
    const text = "Holiday Music";
    if (this.model.dance) {
      breadcrumbs.push({ text, href: "/song/holidaymusic" });
      breadcrumbs.push({ text: toTitleCase(this.model.dance), active: true });
    } else {
      breadcrumbs.push({ text, active: true });
    }
    return breadcrumbs;
  }
}
</script>
