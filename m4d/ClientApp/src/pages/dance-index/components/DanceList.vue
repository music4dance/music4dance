<script setup lang="ts">
import DanceItem from "./DanceItem.vue";
import { DanceGroup } from "@/models/DanceGroup";
import type { NamedObject } from "@/models/NamedObject";
import { TempoType } from "@/models/TempoType";
import { type ColorVariant } from "bootstrap-vue-next";

withDefaults(
  defineProps<{
    dances: NamedObject[];
    showTempo: TempoType;
    flush?: boolean;
    showSynonyms?: boolean;
  }>(),
  { showTempo: TempoType.None, flush: false, showSynonyms: false },
);

function danceVariant(dance: NamedObject): ColorVariant {
  return DanceGroup.isGroup(dance) ? "primary" : "light";
}
</script>

<template>
  <BListGroup :flush="flush">
    <BListGroupItem v-for="(dance, idx) in dances" :key="idx" :variant="danceVariant(dance)">
      <DanceItem :dance="dance" :showTempo="showTempo" />
    </BListGroupItem>
  </BListGroup>
</template>
