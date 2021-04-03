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
import { Component, Mixins, Prop } from "vue-property-decorator";
import { DanceLink, DanceStats } from "@/model/DanceStats";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";

@Component
export default class DanceLinks extends Mixins(EnvironmentManager) {
  @Prop() private readonly danceId!: string;

  private get dance(): DanceStats | undefined {
    return this.environment.fromId(this.danceId);
  }

  private get links(): DanceLink[] {
    return this.dance ? this.dance.danceLinks : [];
  }
}
</script>
