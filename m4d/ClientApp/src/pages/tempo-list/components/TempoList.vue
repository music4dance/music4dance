<script setup lang="ts">
import { defaultTempoLink } from "@/helpers/LinkHelpers";
import { wordsToKebab } from "@/helpers/StringHelpers";
import type { TableFieldRaw, BTableSortBy } from "bootstrap-vue-next";
import type { LiteralUnion } from "@/helpers/bsvn-types";
import { computed, ref } from "vue";
import type { DanceType } from "@/models/DanceDatabase/DanceType";

const props = defineProps<{
  dances: DanceType[];
  hideNameLink?: boolean;
}>();

const sortBy = ref<BTableSortBy[]>([{ key: "name", order: "asc" }]);

const emptyTable = computed(() => {
  return props.dances.length === 0 ? "Please select at least one item from every drop-down" : "";
});

const fields: Exclude<TableFieldRaw<DanceType>, string>[] = [
  {
    key: "name",
    sortable: true,
    sortByFormatted: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      item!.name,
    stickyColumn: true,
  },
  {
    key: "meter",
    sortable: true,
    sortByFormatted: true,
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      item!.meter.toString(),
  },
  {
    key: "bpm",
    label: "BPM",
    sortable: true,
    sortByFormatted: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      item!.tempoRange.min.toLocaleString("en", {
        minimumIntegerDigits: 4,
      }) ?? "",
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      item!.tempoRange.toString() ?? "",
  },
  {
    key: "mpm",
    label: "MPM",
    sortable: true,
    sortByFormatted: (_value: unknown, key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      item!.tempoRange.min.toLocaleString("en", {
        minimumIntegerDigits: 4,
      }) ?? "",
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      item!.tempoRange.mpm(item!.meter.numerator) ?? "",
  },
  {
    key: "groupName",
    label: "Type",
    sortable: true,
    sortByFormatted: true,
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      item!.groups!.map((g) => g.name).join(", "),
  },
  {
    key: "styles",
    sortable: true,
    sortByFormatted: true,
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) => {
      return item!.styles.join(", ") ?? "";
    },
  },
];

function groupLink(dance: DanceType): string {
  return m4dLink(dance.groups![0].name);
}

function styleLink(style: string): string {
  return m4dLink(wordsToKebab(style));
}

function m4dLink(item: string): string {
  return `/dances/${item}`;
}

function formatMPMValue(dance: DanceType): string {
  return dance.tempoRange.mpm(dance.meter.numerator);
}

function formatDefaultValue(dance: DanceType): string {
  return dance.tempoRange.toString();
}

function formatType(dance: DanceType): string {
  return dance.groups.map((g) => g.name).join(", ");
}
</script>

<template>
  <div>
    <BTable
      v-model:sort-by="sortBy"
      striped
      hover
      primary-key="danceId"
      :items="props.dances"
      :fields="fields"
      :caption="emptyTable"
      sort-icon-left
      responsive
    >
      <template #cell(name)="data">
        <DanceName :dance="data.item" :show-synonyms="true"></DanceName>
      </template>
      <template #cell(groupName)="data">
        <a :href="groupLink(data.item)">{{ formatType(data.item) }}</a>
      </template>
      <template #cell(mpm)="data">
        <a :href="defaultTempoLink(data.item)">{{ formatMPMValue(data.item) }}</a>
      </template>
      <template #cell(bpm)="data">
        <a :href="defaultTempoLink(data.item)">{{ formatDefaultValue(data.item) }}</a>
      </template>
      <template #cell(styles)="data">
        <span v-for="(style, index) in data.item.styles" :key="style">
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
