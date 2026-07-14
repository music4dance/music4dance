<script setup lang="ts">
import { TempoType } from "@/models/DanceDatabase/TempoType";
import { ref, computed } from "vue";
import { DanceDatabase } from "@/models/DanceDatabase/DanceDatabase";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import type { DanceObject } from "@/models/DanceDatabase/DanceObject";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";

const nameFilter = ref("");
const showTempo = ref(TempoType.Both);
const dances = safeDanceDatabase();

const filteredDances = computed(() => {
  return DanceDatabase.filterByName(dances.flatGroups, nameFilter.value, true).filter(
    (d) => DanceGroup.isGroup(d) || dances.getSongCount(d.id) > 0,
  ) as DanceObject[];
});
</script>

<template>
  <div>
    <NameFilterInput id="bvt-dance-filter" v-model="nameFilter" placeholder="Filter Dances" />
    <DanceList
      :dances="filteredDances"
      :flush="false"
      :show-tempo="showTempo"
      :show-synonyms="true"
    />
  </div>
</template>
