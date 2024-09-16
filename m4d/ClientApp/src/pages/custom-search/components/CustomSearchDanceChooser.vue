<script setup lang="ts">
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";

const props = defineProps<{ name: string; dance: string; count: number }>();
const dances = safeDanceDatabase().dances;

const sortedDances = dances
  ? dances.filter((d) => d.name !== props.dance).sort((a, b) => a.name.localeCompare(b.name))
  : [];

const danceLink = (dance: string) => `/customsearch?name=${props.name}&dance=${dance}`;
</script>

<template>
  <div>
    <CustomSearchHelp v-if="count && count < 15" :name="name" :dance="dance" />
    <p>Choose a dance style to filter the {{ name }} music to that style</p>
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
