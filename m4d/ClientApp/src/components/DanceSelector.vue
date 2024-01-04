<script setup lang="ts">
import TagSelector from "@/components/TagSelector.vue";
import { NamedObject } from "@/models/NamedObject";

const model = defineModel<string[]>();
const props = defineProps<{
  danceList: NamedObject[];
}>();

const danceOptions = props.danceList
  .map((d) => ({ text: d.name, value: d.id }))
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
  >
  </TagSelector>
</template>
