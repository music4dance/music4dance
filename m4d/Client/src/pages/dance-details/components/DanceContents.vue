<template>
  <b-list-group>
    <b-list-group-item href="#description">Description</b-list-group-item>
    <b-list-group-item
      href="#tempo-info"
      v-if="!dance.isGroup && dance.dance.meter.numerator != 1"
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
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";
import { DanceStats } from "@/model/DanceStats";
import { DanceModel } from "@/model/DanceModel";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";

@Component
export default class DanceContents extends Mixins(EnvironmentManager) {
  @Prop() private readonly model!: DanceModel;

  private get dance(): DanceStats | undefined {
    return this.environment.fromId(this.model.danceId);
  }

  private get danceName(): string | undefined {
    return this.dance?.danceName;
  }

  private get blogLink(): string | undefined {
    const blog = this.dance?.blogTag;
    return blog ? `https://music4dance.blog/tag/${blog}` : undefined;
  }

  private get hasPlayer(): boolean {
    return !!this.dance?.spotifyPlaylist;
  }

  private get hasTopTen(): boolean {
    const histories = this.model.histories;
    return !!histories && !!histories.length && !this.dance?.isGroup;
  }

  private get hasReferences(): boolean {
    return !!this.dance?.danceLinks && this.dance.danceLinks.length > 0;
  }

  private get hasCompetitionInfo(): boolean {
    return (this.dance?.danceType?.competitionDances?.length ?? 0) > 0;
  }
}
</script>
