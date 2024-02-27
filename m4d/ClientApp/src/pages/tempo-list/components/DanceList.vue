<script setup lang="ts">
import { defaultTempoLink } from "@/helpers/LinkHelpers";
import DanceName from "@/components/DanceName.vue";
import { wordsToKebab } from "@/helpers/StringHelpers";
import { Meter } from "@/models/DanceDatabase/Meter";
import type { TableItem, TableFieldRaw } from "bootstrap-vue-next";
import type { LiteralUnion } from "@/helpers/bsvn-types";
import { computed } from "vue";
import type { DanceType } from "@/models/DanceDatabase/DanceType";
import { NamedObject } from "@/models/DanceDatabase/NamedObject";

const props = defineProps<{
  dances: DanceType[];

  hideNameLink?: boolean;
}>();

const items = computed(() => props.dances as TableItem<DanceType>[]);

const emptyTable = computed(() => {
  return items.value.length === 0 ? "Please select at least one item from every drop-down" : "";
});

const fields: Exclude<TableFieldRaw<DanceType>, string>[] = [
  {
    key: "name",
    sortable: true,
    stickyColumn: true,
  },
  {
    key: "meter",
    sortable: true,
    formatter: (value: unknown) => (value as Meter).toString(),
  },
  {
    key: "bpm",
    label: "BPM",
    sortable: true,
    sortByFormatted: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      item?.tempoRange.min.toLocaleString("en", {
        minimumIntegerDigits: 4,
      }) ?? "",
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      item?.tempoRange.toString() ?? "",
  },
  {
    key: "mpm",
    label: "MPM",
    sortable: true,
    sortByFormatted: (_value: unknown, key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      item?.tempoRange.min.toLocaleString("en", {
        minimumIntegerDigits: 4,
      }) ?? "",
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      item?.tempoRange.mpm(item?.meter.numerator) ?? "",
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
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceType>, item?: DanceType) =>
      item?.styles.join(", ") ?? "",
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
      striped
      hover
      primary-key="danceId"
      :items="items"
      :fields="fields"
      :caption="emptyTable"
      sort-by="name"
      sort-icon-left
      responsive
    >
      <template #cell(name)="data">
        <DanceName :dance="data.item as unknown as NamedObject" :show-synonyms="true"></DanceName>
      </template>
      <template #cell(groupName)="data">
        <a :href="groupLink(data.item as unknown as DanceType)">{{ formatType(data.item) }}</a>
      </template>
      <template #cell(mpm)="data">
        <a :href="defaultTempoLink(data.item)">{{ formatMPMValue(data.item) }}</a>
      </template>
      <template #cell(bpm)="data">
        <a :href="defaultTempoLink(data.item as unknown as DanceType)">{{
          formatDefaultValue(data.item)
        }}</a>
      </template>
      <template #cell(styles)="data">
        <span v-for="(style, index) in (data.item as unknown as DanceType).styles" :key="style">
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
