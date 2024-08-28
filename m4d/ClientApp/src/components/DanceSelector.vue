<script setup lang="ts">
import { NamedObject } from "@/models/DanceDatabase/NamedObject";

const model = defineModel<string[]>();
const props = defineProps<{
  danceList: NamedObject[];
}>();

const danceOptions = props.danceList
  .map((d) => ({ text: d.displayName, value: d.id }))
  .sort((a, b) => a.text.localeCompare(b.text));
</script>

<template>
  <TagSelector
    v-model="model"
    :options="danceOptions"
    :show-initial-list="true"
    choose-label="Choose Dances"
    search-label="Search Dances"
    empty-label="No more dances to choose"
    variant="primary"
    class="mt-2"
  />
</template>
