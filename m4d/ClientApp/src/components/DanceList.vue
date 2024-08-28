<script setup lang="ts">
import type { DanceObject } from "@/models/DanceDatabase/DanceObject";
import { TempoType } from "@/models/TempoType";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { computed } from "vue";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";

const props = withDefaults(
  defineProps<{
    dances: DanceObject[];
    showTempo?: number;
    flush?: boolean;
    showSynonyms?: boolean;
  }>(),
  { showTempo: TempoType.None, flush: false, showSynonyms: false },
);
const danceDB = safeDanceDatabase();

const filteredDances = computed(() => {
  return props.dances.filter((d) => DanceGroup.isGroup(d) || danceDB.getSongCount(d.id) > 0);
});

const danceVariant = (dance: DanceObject) => {
  return DanceGroup.isGroup(dance) ? "primary" : "light";
};
</script>

<template>
  <BListGroup :flush="flush">
    <BListGroupItem
      v-for="(dance, idx) in filteredDances"
      :key="idx"
      :variant="danceVariant(dance)"
    >
      <DanceItem :dance="dance" :show-tempo="showTempo" />
    </BListGroupItem>
  </BListGroup>
</template>
