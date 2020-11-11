<template>
  <div id="references">
    <h2>References:</h2>
    <div v-for="link in links" :key="link.link">
      <b>{{ link.description }}: </b><span> </span>
      <a :href="link.link">{{ link.link }}</a>
    </div>
  </div>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Vue, Prop } from "vue-property-decorator";
import TempiLink from "@/components/TempiLink.vue";
import { TypedJSON } from "typedjson";
import { Song } from "@/model/Song";
import { SongFilter } from "@/model/SongFilter";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { DanceLink, DanceStats } from "@/model/DanceStats";

declare const environment: DanceEnvironment;

@Component
export default class DanceLinks extends Vue {
  @Prop() private readonly danceId!: string;

  private get dance(): DanceStats | undefined {
    return environment?.fromId(this.danceId);
  }

  private get links(): DanceLink[] {
    return this.dance ? this.dance.danceLinks : [];
  }
}
</script>
