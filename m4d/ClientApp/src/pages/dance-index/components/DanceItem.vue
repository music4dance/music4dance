<script setup lang="ts">
import { computed } from "vue";
import DanceName from "@/components/DanceName.vue";
import type { NamedObject } from "@/models/NamedObject";
import { DanceGroup } from "@/models/DanceGroup";
import type { DanceTypeCount } from "@/models/DanceTypeCount";
import type { TempoType } from "@/models/TempoType";

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
  return DanceGroup.isGroup(props.dance) ? undefined : (props.dance as DanceTypeCount).count;
});
</script>

<template>
  <div class="d-flex justify-content-between">
    <DanceName :dance="dance" :showTempo="showTempo" :showSynonyms="showSynonyms" />
    <div>
      <b-badge v-if="songCount" :href="countLink" :variant="variant" style="line-height: 1.5"
        >songs ({{ songCount }})</b-badge
      >
      <b-badge :href="danceLink" :variant="variant" style="line-height: 1.5" class="ms-2"
        >info</b-badge
      >
    </div>
  </div>
</template>
