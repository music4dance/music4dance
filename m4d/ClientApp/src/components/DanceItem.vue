<script setup lang="ts">
import { TempoType } from "@/models/TempoType";
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
const songCount = computed(() => {
  return DanceGroup.isGroup(props.dance)
    ? undefined
    : safeDanceDatabase().getSongCount(props.dance.id);
});
const classes = computed(() => {
  return isGroup.value ? "" : "ms-2";
});
</script>

<template>
  <div class="d-flex justify-content-between" :class="classes">
    <DanceName :dance="dance" :show-tempo="showTempo" :show-synonyms="showSynonyms" />
    <div>
      <BBadge v-if="songCount" :href="countLink" :variant="variant" style="line-height: 1.5"
        >songs ({{ songCount }})</BBadge
      >
      <BBadge :href="danceLink" :variant="variant" style="line-height: 1.5" class="ms-2"
        >info</BBadge
      >
    </div>
  </div>
</template>
