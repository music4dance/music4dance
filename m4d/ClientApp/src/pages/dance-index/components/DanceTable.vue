<script setup lang="ts">
import type { DanceCountDatabase } from "@/models/DanceCountDatabase";
import { TempoType } from "@/models/TempoType";
import { ref, computed } from "vue";
import DanceList from "./DanceList.vue";

const props = defineProps<{
  dances: DanceCountDatabase;
}>();

const nameFilter = ref("");
const showTempo = ref(TempoType.Both);

const filteredDances = computed(() => {
  return props.dances.filterByName(nameFilter.value);
});
</script>

<template>
  <div>
    <BInputGroup class="mb-2">
      <BFormInput
        id="bvt-dance-filter"
        type="text"
        v-model="nameFilter"
        placeholder="Filter Dances"
        autofocus
      ></BFormInput>
      <BInputGroupAppend is-text><IBiSearch /></BInputGroupAppend>
    </BInputGroup>
    <DanceList
      :dances="filteredDances"
      :flush="false"
      :showTempo="showTempo"
      :showSynonyms="true"
    />
  </div>
</template>
