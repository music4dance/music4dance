<script setup lang="ts">
import { DanceInstance } from "@/models/DanceDatabase/DanceInstance";
import { danceLink, defaultTempoLink, filteredTempoLink } from "@/helpers/LinkHelpers";
import { Meter } from "@/models/DanceDatabase/Meter";
import { TempoRange } from "@/models/DanceDatabase/TempoRange";
import type { TableItem, TableFieldRaw } from "bootstrap-vue-next";
import { computed } from "vue";

const props = defineProps<{
  dances: DanceInstance[];
  title: string;
  useFullName?: boolean;
}>();

// INT-TODO: This computed is purely to force the cast, shouldn't be necessary
const danceTable = computed(() => props.dances as TableItem<DanceInstance>[]);

const fields: Exclude<TableFieldRaw<DanceInstance>, string>[] = [
  {
    key: "name",
    formatter: (item: any) => name(item),
  },
  {
    key: "mpm",
    label: "MPM",
  },
  {
    key: "dancesport",
    label: "DanceSport",
  },
  {
    key: "ndca",
    label: "NDCA",
  },
  {
    key: "tempoRange",
    label: "BPM",
    formatter: (value: unknown) => (value as TempoRange).toString(),
  },
  {
    key: "meter",
    formatter: (value: unknown) => (value as Meter).toString(),
  },
];

function name(dance: DanceInstance): string {
  return props.useFullName ? dance.name : dance.shortName;
}

// INT-TODO: There is something funky going on with formatting the fields that are MPM tempo ranges
//  so I'm just going to do it manually for now.  My guess is that the formatter isn't being called
//  for slots values.

function formatMPMValue(dance: DanceInstance): string {
  return dance.tempoRange.mpm(dance.meter.numerator) as string;
}

function formatFilteredTempo(dance: DanceInstance, filter: string): string {
  return dance.filteredTempo([filter])!.mpm(dance.meter.numerator);
}
</script>

<template>
  <div>
    <h4 v-if="title">{{ title }}</h4>
    <BTable striped hover :items="danceTable" :fields="fields" responsive>
      <template #cell(name)="data">
        <a :href="danceLink(data.item as unknown as DanceInstance)">{{ data.value }}</a>
      </template>
      <template #cell(mpm)="data">
        <a :href="defaultTempoLink(data.item as unknown as DanceInstance)">{{
          formatMPMValue(data.item)
        }}</a>
      </template>
      <template #cell(dancesport)="data">
        <a :href="filteredTempoLink(data.item as unknown as DanceInstance, 'dancesport')">{{
          formatFilteredTempo(data.item, "dancesport")
        }}</a>
      </template>
      <template #cell(ndca)="data">
        <a :href="filteredTempoLink(data.item as unknown as DanceInstance, 'ndca')">{{
          formatFilteredTempo(data.item, "ndca")
        }}</a>
      </template>
      <template #cell(tempoRange)="data">
        <a :href="defaultTempoLink(data.item as unknown as DanceInstance)">{{ data.value }}</a>
      </template>
    </BTable>
  </div>
</template>
