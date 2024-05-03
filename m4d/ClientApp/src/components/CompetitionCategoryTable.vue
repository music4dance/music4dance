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
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceInstance>, item?: DanceInstance) =>
      di(item).tempoRange.toString(),
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

// INT-TODO: There is something funky going on with formatting the fields that are MPM tempo ranges
//  so I'm just going to do it manually for now.  My guess is that the formatter isn't being called
//  for slots values.

function formatMPMValue(dance: DanceInstance): string {
  return di(dance).tempoRange.mpm(dance.meter.numerator) as string;
}

// The DaneInstance coming in is a flattened version, so we'll retrieve the real one
//  INT-TODO: Probably neeed to declare an IDanceInstance or something
function formatFilteredTempo(dance: DanceInstance, filter: string): string {
  return di(dance).filteredTempo([filter])!.mpm(dance.meter.numerator);
}
</script>

<template>
  <div>
    <h4 v-if="title">{{ title }}</h4>
    <BTable striped hover :items="danceTable" :fields="fields" responsive>
      <template #cell(name)="data">
        <a :href="danceLink(data.item)">{{ data.value }}</a>
      </template>
      <template #cell(mpm)="data">
        <a :href="defaultTempoLink(di(data.item))">{{ formatMPMValue(data.item) }}</a>
      </template>
      <template #cell(dancesport)="data">
        <a :href="filteredTempoLink(di(data.item), 'dancesport')">{{
          formatFilteredTempo(data.item, "dancesport")
        }}</a>
      </template>
      <template #cell(ndca)="data">
        <a :href="filteredTempoLink(di(data.item), 'ndca')">{{
          formatFilteredTempo(data.item, "ndca")
        }}</a>
      </template>
      <template #cell(tempoRange)="data">
        <a :href="defaultTempoLink(di(data.item))">{{ di(data.item).tempoRange.toString() }}</a>
      </template>
    </BTable>
  </div>
</template>
