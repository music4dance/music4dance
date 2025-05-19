<script setup lang="ts">
import type { DanceObject } from "@/models/DanceDatabase/DanceObject";
import { TempoType } from "@/models/TempoType";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";

withDefaults(
  defineProps<{
    dances: DanceObject[];
    showTempo?: number;
    flush?: boolean;
    showSynonyms?: boolean;
  }>(),
  { showTempo: TempoType.None, flush: false, showSynonyms: false },
);

const danceVariant = (dance: DanceObject) => {
  return DanceGroup.isGroup(dance) ? "primary" : "light";
};
</script>

<template>
  <BListGroup :flush="flush">
    <BListGroupItem v-for="(dance, idx) in dances" :key="idx" :variant="danceVariant(dance)">
      <DanceItem :dance="dance" :show-tempo="showTempo" :show-synonyms="showSynonyms" />
    </BListGroupItem>
  </BListGroup>
</template>
