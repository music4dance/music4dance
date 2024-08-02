<script setup lang="ts">
import { computed } from "vue";
import DanceName from "@/components/DanceName.vue";
import type { NamedObject } from "@/models/DanceDatabase/NamedObject";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import type { TempoType } from "@/models/DanceDatabase/TempoType";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";

const props = defineProps<{
  dance: NamedObject;
  showTempo: TempoType;
  showSynonyms?: boolean;
}>();

const danceLink = computed(() => {
  return `/dances/${props.dance.seoName}`;
});

const countLink = computed(() => {
  return `/song/index?filter=.-OOX,${props.dance.id}-Dances`;
});

const variant = computed(() => {
  return DanceGroup.isGroup(props.dance) ? "primary" : "secondary";
});

const songCount = computed(() => {
  return DanceGroup.isGroup(props.dance)
    ? undefined
    : safeDanceDatabase().getSongCount(props.dance.id);
});
</script>

<template>
  <div class="d-flex justify-content-between">
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
