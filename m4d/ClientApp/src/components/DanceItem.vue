<script setup lang="ts">
import { TempoType } from "@/models/TempoType";
import DanceName from "./DanceName.vue";
import type { DanceObject } from "@/models/DanceDatabase/DanceObject";
import { computed } from "vue";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";

const props = withDefaults(
  defineProps<{
    dance: DanceObject;
    showTempo?: number;
    showSynonyms?: boolean;
  }>(),
  { showTempo: TempoType.None, showSynonyms: false },
);

const danceDB = safeDanceDatabase();

const isGroup = computed(() => DanceGroup.isGroup(props.dance));
const danceLink = computed(() => {
  return `/dances/${props.dance.seoName}`;
});
const countLink = computed(() => {
  return `/song/index?filter=.-OOX,${props.dance.id}-Dances`;
});
const variant = computed(() => {
  return isGroup.value ? "secondary" : "primary";
});
</script>

<template>
  <div class="d-flex justify-content-between">
    <DanceName :dance="dance" :show-tempo="showTempo" :show-synonyms="showSynonyms"></DanceName>
    <div>
      <BBadge :href="countLink" :variant="variant" style="line-height: 1.5"
        >songs ({{ danceDB.getSongCount(dance.id) }})</BBadge
      >
      <BBadge :href="danceLink" :variant="variant" style="line-height: 1.5" class="ml-2"
        >info</BBadge
      >
    </div>
  </div>
</template>
