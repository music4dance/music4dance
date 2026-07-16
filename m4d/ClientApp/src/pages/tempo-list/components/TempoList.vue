<script setup lang="ts">
import { defaultTempoLink } from "@/helpers/LinkHelpers";
import { wordsToKebab } from "@/helpers/StringHelpers";
import { filterValid } from "@/models/CheckboxTypes";
import type { TableFieldRaw, BTableSortBy, CheckboxOption } from "bootstrap-vue-next";
import { computed, ref } from "vue";
import type { DanceType } from "@/models/DanceDatabase/DanceType";

const props = defineProps<{
  dances: DanceType[];
  hideNameLink?: boolean;
  // Seeds the column chooser from a server-provided `?columns=` query string (see App.vue's
  // TempoListModel.columns) so a custom column set can be linked to directly. Unknown keys are
  // silently dropped; omit entirely to fall back to each column's own `defaultVisible`.
  initialColumns?: string[];
}>();

const sortBy = ref<BTableSortBy[]>([{ key: "name", order: "asc" }]);

const emptyTable = computed(() => {
  return props.dances.length === 0 ? "Please select at least one item from every drop-down" : "";
});

// Columns an advanced user can hide - "name" is always shown and isn't offered here. New optional
// columns should be added to this list with `defaultVisible: false` so casual users don't see
// their table layout change out from under them.
interface ChooseableColumn {
  key: string;
  label: string;
  defaultVisible: boolean;
}

const chooseableColumns: ChooseableColumn[] = [
  { key: "meter", label: "Meter", defaultVisible: true },
  { key: "bpm", label: "BPM", defaultVisible: true },
  { key: "mpm", label: "MPM", defaultVisible: true },
  { key: "groupName", label: "Type", defaultVisible: true },
  { key: "styles", label: "Styles", defaultVisible: true },
  { key: "validationRange", label: "Range", defaultVisible: false },
];

const columnOptions: CheckboxOption[] = chooseableColumns.map((c) => ({
  text: c.label,
  value: c.key,
}));

const visibleColumns = ref<string[]>(
  props.initialColumns
    ? filterValid(
        chooseableColumns.map((c) => c.key),
        props.initialColumns,
      )
    : chooseableColumns.filter((c) => c.defaultVisible).map((c) => c.key),
);

const allFields: Exclude<TableFieldRaw<DanceType>, string>[] = [
  {
    key: "name",
    sortable: true,
    sortByFormatted: ({ item }: { value: unknown; key: string; item: DanceType }) => item.name,
    stickyColumn: true,
  },
  {
    key: "meter",
    sortable: true,
    sortByFormatted: true,
    formatter: ({ item }: { value: unknown; key: string; item: DanceType }) => {
      return item?.meter?.toString() ?? "";
    },
  },
  {
    key: "bpm",
    label: "BPM",
    sortable: true,
    sortByFormatted: ({ item }: { value: unknown; key: string; item: DanceType }) => {
      return (
        item?.tempoRange?.min.toLocaleString("en", {
          minimumIntegerDigits: 4,
        }) ?? ""
      );
    },
    formatter: ({ item }: { value: unknown; key: string; item: DanceType }) => {
      return item?.tempoRange?.toString() ?? "";
    },
  },
  {
    key: "mpm",
    label: "MPM",
    sortable: true,
    sortByFormatted: ({ item }: { value: unknown; key: string; item: DanceType }) => {
      return (
        item?.tempoRange?.min.toLocaleString("en", {
          minimumIntegerDigits: 4,
        }) ?? ""
      );
    },
    formatter: ({ item }: { value: unknown; key: string; item: DanceType }) => {
      return item?.tempoRange?.mpm(item?.meter?.numerator ?? 1) ?? "";
    },
  },
  {
    key: "groupName",
    label: "Type",
    sortable: true,
    sortByFormatted: true,
    formatter: ({ item }: { value: unknown; key: string; item: DanceType }) => {
      return item?.groups?.map((g) => g.name).join(", ") ?? "";
    },
  },
  {
    key: "styles",
    sortable: true,
    sortByFormatted: true,
    formatter: ({ item }: { value: unknown; key: string; item: DanceType }) => {
      return item?.styles?.join(", ") ?? "";
    },
  },
  {
    key: "validationRange",
    label: "Range",
    sortable: true,
    sortByFormatted: ({ item }: { value: unknown; key: string; item: DanceType }) => {
      return (
        item?.validationRange?.min.toLocaleString("en", {
          minimumIntegerDigits: 4,
        }) ?? ""
      );
    },
    formatter: ({ item }: { value: unknown; key: string; item: DanceType }) => {
      return item?.validationRange?.toString() ?? "";
    },
  },
];

const fields = computed(() =>
  allFields.filter((f) => f.key === "name" || visibleColumns.value.includes(f.key as string)),
);

function groupLink(dance: DanceType): string {
  return m4dLink(dance.groups?.[0]?.name || "");
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
        <DanceName :dance="data.item" :show-synonyms="true" show-blog-link />
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
    <div class="d-flex align-items-center gap-2">
      <span class="text-muted small">Columns:</span>
      <CheckedList
        v-model="visibleColumns"
        type="Column"
        :options="columnOptions"
        variant="outline-secondary"
        size="sm"
      />
    </div>
    <p v-if="visibleColumns.includes('validationRange')" class="text-muted small mt-2 mb-2">
      <strong>Range</strong> is the broadest tempo range we consider plausible for this dance style
      before assuming a reported tempo is a half-time/double-time detection error - not the dance's
      typical tempo.
    </p>
  </div>
</template>
