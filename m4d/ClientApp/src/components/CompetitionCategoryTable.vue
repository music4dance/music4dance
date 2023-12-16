<script setup lang="ts">
import { wordsToKebab } from "@/helpers/StringHelpers";
import { DanceInstance } from "@/models/DanceInstance";
import { Meter } from "@/models/Meter";
import { TempoRange } from "@/models/TempoRange";
import type { TableItem, TableField } from "bootstrap-vue-next";
import { computed } from "vue";

const props = defineProps<{
  dances: DanceInstance[];
  title: string;
  useFullName?: boolean;
}>();

// INT-TODO: This computed is purely to force the cast, shouldn't be necessary
const danceTable = computed(() => props.dances as unknown as TableItem[]);

const styleFamily = computed(() => {
  if (!props.dances || props.dances.length === 0) {
    return "both";
  }
  const family = props.dances[0].styleFamily;
  return props.dances.every((d) => d.styleFamily === family) ? family : "Both";
});

const ndcaATitle = computed(() => {
  const family = styleFamily.value;
  switch (family) {
    case "American":
      return "NDCA Silver/Gold";
    case "International":
      return "NDCA Professional or Amateur";
    default:
      return "NDCA A(*)";
  }
});

const ndcaBTitle = computed(() => {
  const family = styleFamily.value;
  switch (family) {
    case "American":
      return "NDCA Bronze";
    case "International":
      return "NDCA Pro/Am";
    default:
      return "NDCA B(*)";
  }
});

const isMixed = computed(() => {
  return styleFamily.value === "Both";
});

// INT-TODO: We should be able to use TableField<DanceInstance> but it doesn't work
// until at the least the bsv table types are templated
// : TableField<DanceInstance>[]
const fields = [
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
    key: "ndca-1",
    label: ndcaATitle.value,
  },
  {
    key: "ndca-2",
    label: ndcaBTitle.value,
  },
  {
    key: "tempoRange",
    label: "BPM",
    formatter: (value: TempoRange) => value.toString(),
  },
  {
    key: "meter",
    formatter: (value: Meter) => value.toString(),
  },
] as unknown as TableField[];

function name(dance: DanceInstance): string {
  return props.useFullName ? dance.name : dance.shortName;
}

function danceLink(dance: DanceInstance): string {
  return wordsToKebab(dance.shortName);
}

function tempoLink(dance: DanceInstance, tempo: TempoRange): string {
  return `/song/advancedsearch?dances=${dance.baseId}&tempomin=${tempo.min}&tempomax=${tempo.max}`;
}

function defaultTempoLink(dance: DanceInstance): string {
  return tempoLink(dance, dance.tempoRange);
}

function filteredTempoLink(dance: DanceInstance, filter: string): string {
  return tempoLink(dance, dance.filteredTempo([filter])!);
}

// INT-TODO: There is something funky going on with formatting the fields that are MPM temporanges
//  so I'm just going to do it manually for now

function formatMPMValue(item: TableItem): string {
  const dance = item as unknown as DanceInstance;
  return dance.tempoRange.mpm(dance.meter.numerator) as string;
}

function formatFilteredTempo(item: TableItem, filter: string): string {
  const dance = item as unknown as DanceInstance;
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
      <template #cell(ndca-1)="data">
        <a :href="filteredTempoLink(data.item as unknown as DanceInstance, 'ndca-1')">{{
          formatFilteredTempo(data.item, "ndca-1")
        }}</a>
      </template>
      <template #cell(ndca-2)="data">
        <a :href="filteredTempoLink(data.item as unknown as DanceInstance, 'ndca-2')">{{
          formatFilteredTempo(data.item, "ndca-2")
        }}</a>
      </template>
      <template #cell(tempoRange)="data">
        <a :href="defaultTempoLink(data.item as unknown as DanceInstance)">{{ data.value }}</a>
      </template>
    </BTable>
    <p v-if="isMixed">
      (*) A short explanation of the NDCA (<a
        href="https://www.ndca.org/pages/ndca_rule_book/Default.asp"
        >National Dance Council of America)</a
      >
      columns: For American style dances the "A" column contains Tempi for Silver and Gold levels
      while the "B" column contains Tempi for Bronze level. For International style dances the "A"
      column contains Tempi for Professional and Amateur couples while the "B" column contains Tempi
      for Pro/Am couples.
    </p>
  </div>
</template>
