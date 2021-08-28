<template>
  <div>
    <holiday-help v-if="count && count < 15" :dance="dance"></holiday-help>
    <p>Choose a dance style to filter the holiday music to that style</p>
    <b-button
      v-for="dance in sortedDances"
      :key="dance.id"
      :href="danceLink(dance.name)"
      variant="primary"
      style="margin: 0.25em"
      >{{ dance.name }}</b-button
    >
  </div>
</template>

<script lang="ts">
import "reflect-metadata";
import HolidayHelp from "./HolidayHelp.vue";
import { Component, Prop, Mixins } from "vue-property-decorator";
import { DanceStats } from "@/model/DanceStats";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";

@Component({
  components: {
    HolidayHelp,
  },
})
export default class HolidayDanceChooser extends Mixins(EnvironmentManager) {
  @Prop() private readonly dance!: string;
  @Prop() private readonly count!: number;

  private get sortedDances(): DanceStats[] {
    const environment = this.environment;
    return environment
      ? environment.flatStats
          .filter((d) => d.songCount > 10 && d.name !== this.dance)
          .sort((a, b) => a.name.localeCompare(b.name))
      : [];
  }

  private danceLink(dance: string): string {
    return `/song/holidaymusic?dance=${dance}`;
  }
}
</script>
