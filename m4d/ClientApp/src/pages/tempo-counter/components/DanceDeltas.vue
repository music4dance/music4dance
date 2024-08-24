<script setup lang="ts">
import { computed } from "vue";
import type { DanceType } from "@/models/DanceDatabase/DanceType";
import { DanceFilter } from "@/models/DanceDatabase/DanceFilter";
import { Meter } from "@/models/DanceDatabase/Meter";
import { DanceDatabase } from "@/models/DanceDatabase/DanceDatabase";

const props = defineProps<{
  dances: DanceType[];
  beatsPerMinute: number;
  beatsPerMeasure: number;
  epsilonPercent: number;
  tempoType: number;
  hideNameLink: boolean;
}>();

const duplefilter = new DanceFilter({ meters: [new Meter(2, 4), new Meter(4, 4)] });
const triplefilter = new DanceFilter({ meters: [new Meter(3, 4)] });

const meterFilter = function (numerator: number) {
  switch (numerator) {
    case 2:
    case 4:
      return duplefilter;
    case 3:
      return triplefilter;
    default:
      return undefined;
  }
};
const orderedDances = computed(() => {
  let dances = props.dances;
  const filter = meterFilter(props.beatsPerMeasure);
  if (filter) {
    dances = filter.filter(dances);
  }
  return DanceDatabase.filterTempo(dances, props.beatsPerMinute, props.epsilonPercent);
});
</script>

<template>
  <div>
    <BListGroup v-for="ds in orderedDances" :key="ds.dance.id">
      <TempoDeltaInfo
        :dance="ds"
        :tempo-type="tempoType"
        :hide-link="hideNameLink"
        v-bind="$attrs"
      />
    </BListGroup>
  </div>
</template>
