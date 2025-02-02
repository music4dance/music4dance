<script setup lang="ts">
import { DanceInstance } from "@/models/DanceDatabase/DanceInstance";
import { danceLink, defaultTempoLink, filteredTempoLink } from "@/helpers/LinkHelpers";
import type { TableFieldRaw } from "bootstrap-vue-next";
import type { LiteralUnion } from "@/helpers/bsvn-types";

const props = defineProps<{
  dances: DanceInstance[];
  title: string;
  useFullName?: boolean;
}>();

const fields: Exclude<TableFieldRaw<DanceInstance>, string>[] = [
  {
    key: "name",
    formatter: (item: unknown) => name(item as DanceInstance),
  },
  {
    key: "dancesport_mpm",
    label: "DanceSport (MPM)",
  },
  {
    key: "dancesport_bpm",
    label: "DanceSport (BPM)",
  },
  {
    key: "ndca_mpm",
    label: "NDCA (MPM)",
  },
  {
    key: "ndca_bpm",
    label: "NDCA (BPM)",
  },
  {
    key: "meter",
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceInstance>, item?: DanceInstance) =>
      item!.meter.toString(),
  },
];

function name(dance: DanceInstance): string {
  return props.useFullName ? dance.name : dance.shortName;
}

// The DaneInstance coming in is a flattened version, so we'll retrieve the real one
//  INT-TODO: Probably neeed to declare an IDanceInstance or something
function formatFilteredMpm(dance: DanceInstance, filter: string): string {
  return dance.filteredTempo([filter])!.mpm(dance.meter.numerator);
}

function formatFilteredBpm(dance: DanceInstance, filter: string): string {
  return dance.filteredTempo([filter])!.toString();
}
</script>

<template>
  <div>
    <h4 v-if="title">{{ title }}</h4>
    <BTable striped hover :items="props.dances" :fields="fields" responsive>
      <template #cell(name)="data">
        <a :href="danceLink(data.item)">{{ name(data.item) }}</a>
      </template>
      <template #cell(dancesport_mpm)="data">
        <a :href="filteredTempoLink(data.item, 'dancesport')">{{
          formatFilteredMpm(data.item, "dancesport")
        }}</a>
      </template>
      <template #cell(dancesport_bpm)="data">
        <a :href="filteredTempoLink(data.item, 'dancesport')">{{
          formatFilteredBpm(data.item, "dancesport")
        }}</a>
      </template>
      <template #cell(ndca_mpm)="data">
        <a :href="filteredTempoLink(data.item, 'ndca')">{{
          formatFilteredMpm(data.item, "ndca")
        }}</a>
      </template>
      <template #cell(ndca_bpm)="data">
        <a :href="filteredTempoLink(data.item, 'ndca')">{{
          formatFilteredBpm(data.item, "ndca")
        }}</a>
      </template>
      <template #cell(tempoRange)="data">
        <a :href="defaultTempoLink(data.item)">{{ data.item.tempoRange.toString() }}</a>
      </template>
    </BTable>
  </div>
</template>
