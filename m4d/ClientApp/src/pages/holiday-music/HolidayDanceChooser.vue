<script setup lang="ts">
import HolidayHelp from "./HolidayHelp.vue";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";

const props = defineProps<{ occassion: string; dance: string; count: number }>();
const dances = safeDanceDatabase().dances;

const sortedDances = dances
  ? dances.filter((d) => d.name !== props.dance).sort((a, b) => a.name.localeCompare(b.name))
  : [];

const danceLink = (dance: string) =>
  `/song/holidaymusic?occassion=${props.occassion}&dance=${dance}`;
</script>

<template>
  <div>
    <HolidayHelp v-if="count && count < 15" :dance="dance"></HolidayHelp>
    <p>Choose a dance style to filter the holiday music to that style</p>
    <BButton
      v-for="d in sortedDances"
      :key="d.id"
      :href="danceLink(d.name)"
      variant="primary"
      style="margin: 0.25em"
      >{{ d.name }}</BButton
    >
  </div>
</template>
