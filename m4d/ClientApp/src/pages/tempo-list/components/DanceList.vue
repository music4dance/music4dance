<script setup lang="ts">
import { defaultTempoLink } from "@/helpers/LinkHelpers";
import DanceName from "@/components/DanceName.vue";
import { wordsToKebab } from "@/helpers/StringHelpers";
import type { TableItem, TableFieldRaw, BTableSortBy } from "bootstrap-vue-next";
import type { LiteralUnion } from "@/helpers/bsvn-types";
import { computed, ref } from "vue";
import type { DanceType } from "@/models/DanceDatabase/DanceType";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";

const props = defineProps<{
  dances: DanceType[];
  hideNameLink?: boolean;
}>();

const danceDB = safeDanceDatabase();

// INT-TODO: If I get something through to pipe the original DanceInstance through the system,
//  I can remove the dt() function and just use the original dance object - this
//  computed may not be necessary at all
const items = computed(() => props.dances.map((d) => d.toJSON()) as TableItem<DanceType>[]);

const sortBy = ref<BTableSortBy[]>([{ key: "name", order: "asc" }]);

const emptyTable = computed(() => {
  return items.value.length === 0 ? "Please select at least one item from every drop-down" : "";
});

const fields: Exclude<TableFieldRaw<DanceType>, string>[] = [
  {
    key: "name",
    sortable: true,
    sortByFormatted: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      dt(item!).name,
    stickyColumn: true,
  },
  {
    key: "meter",
    sortable: true,
    sortByFormatted: true,
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      dt(item).meter.toString(),
  },
  {
    key: "bpm",
    label: "BPM",
    sortable: true,
    sortByFormatted: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      dt(item!).tempoRange.min.toLocaleString("en", {
        minimumIntegerDigits: 4,
      }) ?? "",
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      dt(item).tempoRange.toString() ?? "",
  },
  {
    key: "mpm",
    label: "MPM",
    sortable: true,
    sortByFormatted: (_value: unknown, key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      dt(item!).tempoRange.min.toLocaleString("en", {
        minimumIntegerDigits: 4,
      }) ?? "",
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      dt(item).tempoRange.mpm(dt(item).meter.numerator) ?? "",
  },
  {
    key: "groupName",
    label: "Type",
    sortable: true,
    sortByFormatted: true,
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      dt(item!)
        .groups!.map((g) => g.name)
        .join(", "),
  },
  {
    key: "styles",
    sortable: true,
    sortByFormatted: true,
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) => {
      console.log("item", item);
      return dt(item).styles.join(", ") ?? "";
    },
  },
];

function dt(dance?: DanceType): DanceType {
  if (!dance) {
    throw new Error("Dance undefined");
  }
  const d = danceDB.danceFromId(dance.internalId);
  if (!d) {
    throw new Error(`Dance not found: ${dance.internalId}`);
  }
  return d;
}

function groupLink(dance: DanceType): string {
  return m4dLink(dt(dance).groups![0].name);
}

function styleLink(style: string): string {
  return m4dLink(wordsToKebab(style));
}

function m4dLink(item: string): string {
  return `/dances/${item}`;
}

function formatMPMValue(dance: DanceType): string {
  return dt(dance).tempoRange.mpm(dt(dance).meter.numerator);
}

function formatDefaultValue(dance: DanceType): string {
  return dt(dance).tempoRange.toString();
}

function formatType(dance: DanceType): string {
  return dt(dance)
    .groups.map((g) => g.name)
    .join(", ");
}
</script>

<template>
  <div>
    <div>{{ sortBy }}</div>
    <BTable
      v-model:sort-by="sortBy"
      striped
      hover
      primary-key="danceId"
      :items="items"
      :fields="fields"
      :caption="emptyTable"
      sort-icon-left
      responsive
    >
      <template #cell(name)="data">
        <DanceName :dance="dt(data.item)" :show-synonyms="true"></DanceName>
      </template>
      <template #cell(groupName)="data">
        <a :href="groupLink(dt(data.item))">{{ formatType(data.item) }}</a>
      </template>
      <template #cell(mpm)="data">
        <a :href="defaultTempoLink(dt(data.item))">{{ formatMPMValue(data.item) }}</a>
      </template>
      <template #cell(bpm)="data">
        <a :href="defaultTempoLink(dt(data.item))">{{ formatDefaultValue(data.item) }}</a>
      </template>
      <template #cell(styles)="data">
        <span v-for="(style, index) in dt(data.item).styles" :key="style">
          <span v-if="index !== 0">, </span>
          <a v-if="style.indexOf(' ') !== -1" :href="styleLink(style)">
            {{ style }}
          </a>
          <span v-else>
            {{ style }}
          </span>
        </span>
      </template>
    </BTable>
  </div>
</template>
