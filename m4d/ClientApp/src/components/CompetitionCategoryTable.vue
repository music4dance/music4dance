<script setup lang="ts">
import { DanceInstance } from "@/models/DanceDatabase/DanceInstance";
import { danceLink, defaultTempoLink, filteredTempoLink } from "@/helpers/LinkHelpers";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import type { TableItem, TableFieldRaw } from "bootstrap-vue-next";
import { computed } from "vue";
import type { LiteralUnion } from "@/helpers/bsvn-types";

const props = defineProps<{
  dances: DanceInstance[];
  title: string;
  useFullName?: boolean;
}>();

const danceDB = safeDanceDatabase();

// INT-TODO: If I get something through to pipe the original DanceInstance through the system,
//  I can remove the di() function and just use the original dance object - this
//  computed may not be necessary at all
const danceTable = computed(
  () => props.dances.map((d) => d.toJSON()) as TableItem<DanceInstance>[],
);

const fields: Exclude<TableFieldRaw<DanceInstance>, string>[] = [
  {
    key: "name",
    formatter: (item: any) => name(item),
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
      di(item).meter.toString(),
  },
];

function name(dance: DanceInstance): string {
  return props.useFullName ? dance.name : dance.shortName;
}

function di(dance?: DanceInstance): DanceInstance {
  if (!dance) {
    throw new Error("Dance instance is undefined");
  }
  const d = danceDB.instanceFromId(dance.id);
  if (!d) {
    throw new Error(`Could not find dance instance with id ${dance.id}`);
  }
  return d;
}

// The DaneInstance coming in is a flattened version, so we'll retrieve the real one
//  INT-TODO: Probably neeed to declare an IDanceInstance or something
function formatFilteredMpm(dance: DanceInstance, filter: string): string {
  return di(dance).filteredTempo([filter])!.mpm(dance.meter.numerator);
}

function formatFilteredBpm(dance: DanceInstance, filter: string): string {
  return di(dance).filteredTempo([filter])!.toString();
}
</script>

<template>
  <div>
    <h4 v-if="title">{{ title }}</h4>
    <BTable striped hover :items="danceTable" :fields="fields" responsive>
      <template #cell(name)="data">
        <a :href="danceLink(data.item)">{{ name(data.item) }}</a>
      </template>
      <template #cell(dancesport_mpm)="data">
        <a :href="filteredTempoLink(di(data.item), 'dancesport')">{{
          formatFilteredMpm(data.item, "dancesport")
        }}</a>
      </template>
      <template #cell(dancesport_bpm)="data">
        <a :href="filteredTempoLink(di(data.item), 'dancesport')">{{
          formatFilteredBpm(data.item, "dancesport")
        }}</a>
      </template>
      <template #cell(ndca_mpm)="data">
        <a :href="filteredTempoLink(di(data.item), 'ndca')">{{
          formatFilteredMpm(data.item, "ndca")
        }}</a>
      </template>
      <template #cell(ndca_bpm)="data">
        <a :href="filteredTempoLink(di(data.item), 'ndca')">{{
          formatFilteredBpm(data.item, "ndca")
        }}</a>
      </template>
      <template #cell(tempoRange)="data">
        <a :href="defaultTempoLink(di(data.item))">{{ di(data.item).tempoRange.toString() }}</a>
      </template>
    </BTable>
  </div>
</template>
