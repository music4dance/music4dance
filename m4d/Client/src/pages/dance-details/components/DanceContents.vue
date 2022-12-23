<template>
  <b-list-group>
    <b-list-group-item href="#description">Description</b-list-group-item>
    <b-list-group-item
      href="#tempo-info"
      v-if="!dance.isGroup && dance.meter.numerator != 1"
      >Tempo Info</b-list-group-item
    >
    <b-list-group-item href="#top-ten" v-if="hasTopTen"
      >Top Ten Songs</b-list-group-item
    >
    <b-list-group-item href="#spotify-player" v-if="hasPlayer"
      >{{ danceName }} music on Spotify</b-list-group-item
    >
    <b-list-group-item href="#dance-styles" v-if="dance.isGroup"
      >Dance Styles</b-list-group-item
    >
    <b-list-group-item href="#references" v-if="hasReferences"
      >References</b-list-group-item
    >
    <b-list-group-item href="#competition-info" v-if="hasCompetitionInfo"
      >Competition Info</b-list-group-item
    >
    <b-list-group-item :href="model.filter.url"
      >All {{ danceName }} Songs
      <b-icon-box-arrow-up-right></b-icon-box-arrow-up-right
    ></b-list-group-item>
    <b-list-group-item href="#tags" v-if="!dance.isGroup"
      >Tags</b-list-group-item
    >
    <b-list-group-item v-if="blogLink" :href="blogLink"
      >Blog <b-icon-box-arrow-up-right></b-icon-box-arrow-up-right
    ></b-list-group-item>
  </b-list-group>
</template>

<script lang="ts">
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceModel } from "@/model/DanceModel";
import { DanceStats } from "@/model/DanceStats";
import { TypeStats } from "@/model/TypeStats";
import "reflect-metadata";
import { PropType } from "vue";

export default EnvironmentManager.extend({
  props: { model: { type: Object as PropType<DanceModel>, required: true } },
  computed: {
    dance(): DanceStats | undefined {
      return this.environment.fromId(this.model.danceId);
    },
    danceName(): string | undefined {
      return this.dance?.name;
    },
    blogLink(): string | undefined {
      const blog = this.dance?.blogTag;
      return blog ? `https://music4dance.blog/tag/${blog}` : undefined;
    },
    hasPlayer(): boolean {
      return !!this.model.spotifyPlaylist;
    },
    hasTopTen(): boolean {
      const histories = this.model.histories;
      return !!histories && !!histories.length && !this.dance?.isGroup;
    },
    hasReferences(): boolean {
      return !!this.model.links && this.model.links.length > 0;
    },
    hasCompetitionInfo(): boolean {
      const dance = this.dance;
      return (
        (!!dance &&
          !dance.isGroup &&
          (dance as TypeStats).competitionDances?.length) > 0
      );
    },
  },
});
</script>
