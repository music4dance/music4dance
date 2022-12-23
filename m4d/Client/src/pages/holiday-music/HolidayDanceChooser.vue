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
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceStats } from "@/model/DanceStats";
import "reflect-metadata";
import HolidayHelp from "./HolidayHelp.vue";

export default EnvironmentManager.extend({
  components: { HolidayHelp },
  props: {
    dance: String,
    count: Number,
  },
  data() {
    return new (class {})();
  },
  computed: {
    sortedDances(): DanceStats[] {
      const dances = this.environment?.dances;
      return dances
        ? dances
            .filter((d) => d.songCount > 10 && d.name !== this.dance)
            .sort((a, b) => a.name.localeCompare(b.name))
        : [];
    },
  },
  methods: {
    danceLink(dance: string): string {
      return `/song/holidaymusic?dance=${dance}`;
    },
  },
});
</script>
