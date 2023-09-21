<template>
  <page id="app">
    <h1>{{ title }}</h1>
    <holiday-help v-if="model.count === 0" :dance="model.dance" :empty="true">
    </holiday-help>
    <div v-else>
      <p v-if="model.dance">
        This page includes a list of all of the
        <a :href="danceLink">{{ danceName }}</a> music on the site that has been
        tagged as {{ model.description }}. Click <a :href="allLink">here</a> to
        see {{ model.occassion }} music for all dance styles.
      </p>
      <p v-else>
        This page includes a list of all of the
        <a href="/dances/ballroom-competition-categories">Ballroom</a> and other
        <a href="/dances">partner dance</a> music on the site that has been
        tagged as {{ model.description }}.
      </p>
      <song-table
        :histories="model.histories"
        :filter="model.filter"
        :hideSort="true"
        :hiddenColumns="['Track']"
      ></song-table>
      <song-footer :model="model" :href="pageLink"></song-footer>
    </div>
    <spotify-player :playlist="model.playListId"></spotify-player>
    <holiday-dance-chooser
      :occassion="model.occassion"
      :dance="model.dance"
      :count="model.count"
    ></holiday-dance-chooser>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import SongFooter from "@/components/SongFooter.vue";
import SongTable from "@/components/SongTable.vue";
import SpotifyPlayer from "@/components/SpotifyPlayer.vue";
import { safeEnvironment } from "@/helpers/DanceEnvironmentManager";
import { toTitleCase, wordsToKebab } from "@/helpers/StringHelpers";
import { BreadCrumbItem, homeCrumb, songCrumb } from "@/model/BreadCrumbItem";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import { HolidaySongListModel } from "@/model/HolidaySongListModel";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import Vue from "vue";
import HolidayDanceChooser from "./HolidayDanceChooser.vue";
import HolidayHelp from "./HolidayHelp.vue";

declare const model: string;

export default Vue.extend({
  components: {
    HolidayDanceChooser,
    HolidayHelp,
    Page,
    SongFooter,
    SongTable,
    SpotifyPlayer,
  },
  data() {
    return new (class {
      model: HolidaySongListModel = TypedJSON.parse(
        model,
        HolidaySongListModel
      )!;
      environment: DanceEnvironment = safeEnvironment();
    })();
  },
  computed: {
    title(): string {
      const occassion = this.occassion;
      return this.model.dance
        ? `${occassion} ${toTitleCase(this.model.dance)} Music`
        : `${occassion} Dance Music`;
    },
    occassion(): string {
      return toTitleCase(this.model.occassion);
    },
    allLink(): string {
      const occassion = this.model.occassion;
      return `/song/holidaymusic?occassion=${occassion}`;
    },
    pageLink(): string {
      const dance = this.model.dance;
      return dance ? `${this.allLink}&dance=${dance}` : this.allLink;
    },
    danceLink(): string {
      return `/dances/${this.seoDanceName}`;
    },
    danceName(): string | undefined {
      const dance = this.model.dance;
      return dance ? toTitleCase(dance) : undefined;
    },
    seoDanceName(): string | undefined {
      const dance = this.model.dance;
      return dance ? wordsToKebab(dance) : undefined;
    },
    breadcrumbs(): BreadCrumbItem[] {
      const breadcrumbs = [homeCrumb, songCrumb];
      const text = `${this.occassion} Music`;
      if (this.model.dance) {
        breadcrumbs.push({ text, href: this.allLink });
        breadcrumbs.push({ text: toTitleCase(this.model.dance), active: true });
      } else {
        breadcrumbs.push({ text, active: true });
      }
      return breadcrumbs;
    },
  },
});
</script>
