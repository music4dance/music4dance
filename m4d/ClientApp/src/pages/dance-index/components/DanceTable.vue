<script setup lang="ts">
import { TempoType } from "@/models/DanceDatabase/TempoType";
import { ref, computed } from "vue";
import type { DanceDatabase } from "@/models/DanceDatabase/DanceDatabase";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import type { DanceObject } from "@/models/DanceDatabase/DanceObject";

const props = defineProps<{
  dances: DanceDatabase;
}>();

const nameFilter = ref("");
const showTempo = ref(TempoType.Both);

const filteredDances = computed(() => {
  return props.dances
    .filterByName(nameFilter.value)
    .filter((d) => DanceGroup.isGroup(d) || props.dances.getSongCount(d.id) > 0) as DanceObject[];
});
</script>

<template>
  <div>
    <BInputGroup class="mb-2">
      <BFormInput
        id="bvt-dance-filter"
        v-model="nameFilter"
        type="text"
        placeholder="Filter Dances"
        autofocus
      ></BFormInput>
      <span><IBiSearch /></span>
    </BInputGroup>
    <DanceList
      :dances="filteredDances"
      :flush="false"
      :show-tempo="showTempo"
      :show-synonyms="true"
    />
  </div>
</template>
