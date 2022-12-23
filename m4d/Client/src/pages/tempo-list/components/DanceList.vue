<template>
  <div>
    <b-table
      striped
      hover
      primary-key="danceId"
      :items="dances"
      :fields="fields"
      :filter="filter"
      :filter-function="doFilter"
      :caption="emptyTable"
      @filtered="onFiltered"
      sort-by="name"
      sort-icon-left
      responsive
    >
      <template v-slot:cell(name)="data">
        <dance-name :dance="data.item" :showSynonyms="true"></dance-name>
      </template>
      <template v-slot:cell(groupName)="data">
        <a :href="groupLink(data.item)">{{ data.value }}</a>
      </template>
      <template v-slot:cell(mpm)="data">
        <a :href="tempoLink(data.item)">{{ data.value }}</a>
      </template>
      <template v-slot:cell(bpm)="data">
        <a :href="tempoLink(data.item)">{{ data.value }}</a>
      </template>
      <template v-slot:cell(styles)="data">
        <span
          v-for="(style, index) in data.item.filteredStyles(styles)"
          :key="style"
        >
          <span v-if="index !== 0">, </span>
          <a v-if="style.indexOf(' ') !== -1" :href="styleLink(style)">
            {{ style }}
          </a>
          <span v-else>
            {{ style }}
          </span>
        </span>
      </template>
    </b-table>
  </div>
</template>

<script lang="ts">
import DanceName from "@/components/DanceName.vue";
import { wordsToKebab } from "@/helpers/StringHelpers";
import { DanceFilter } from "@/model/DanceFilter";
import { Meter } from "@/model/Meter";
import { TempoRange } from "@/model/TempoRange";
import { TypeStats } from "@/model/TypeStats";
import { BvTableFieldArray } from "bootstrap-vue";
import "reflect-metadata";
import Vue, { PropType } from "vue";

export default Vue.extend({
  components: { DanceName },
  props: {
    dances: { type: [] as PropType<TypeStats>, required: true },
    styles: { type: [] as PropType<string[]>, required: true },
    meters: { type: [] as PropType<string[]>, required: true },
    types: { type: [] as PropType<string[]>, required: true },
    organizations: { type: [] as PropType<string[]>, required: true },

    allStyles: { type: [] as PropType<string[]>, required: true },
    allMeters: { type: [] as PropType<string[]>, required: true },
    allTypes: { type: [] as PropType<string[]>, required: true },

    hideNameLink: { type: Boolean, required: false },
  },
  data() {
    return new (class {
      emptyTable = "";
    })();
  },
  computed: {
    fields(): BvTableFieldArray {
      return [
        {
          key: "name",
          sortable: true,
          stickyColumn: true,
        },
        {
          key: "meter",
          sortable: true,
          formatter: (value: Meter) => value.toString(),
        },
        {
          key: "bpm",
          label: "BPM",
          sortable: true,
          sortByFormatted: (value: TempoRange, key: string, item: TypeStats) =>
            this.filteredTempo(item).min.toLocaleString("en", {
              minimumIntegerDigits: 4,
            }),
          formatter: (value: TempoRange, key: string, item: TypeStats) =>
            this.filteredTempo(item).toString(),
        },
        {
          key: "mpm",
          label: "MPM",
          sortable: true,
          sortByFormatted: (value: TempoRange, key: string, item: TypeStats) =>
            this.filteredTempo(item).min.toLocaleString("en", {
              minimumIntegerDigits: 4,
            }),
          formatter: (value: TempoRange, key: string, item: TypeStats) =>
            this.filteredTempo(item).mpm(item.meter.numerator),
        },
        {
          key: "groupName",
          label: "Type",
          sortable: true,
          formatter: (value: undefined, key: string, item: TypeStats) =>
            item.groups!.map((g) => g.name).join(", "),
        },
        {
          key: "styles",
          sortable: true,
          formatter: (value: undefined, key: string, item: TypeStats) =>
            item.filteredStyles(this.styles).join(", "),
        },
      ];
    },
    unfiltered(): string {
      const filter = {
        styles: this.allStyles,
        meters: this.allMeters,
        types: this.allTypes,
      };

      return JSON.stringify(filter);
    },
    filter(): DanceFilter | null {
      const filter = {
        styles: this.styles,
        meters: this.meters,
        types: this.types,
      };

      return JSON.stringify(filter) === this.unfiltered ? null : filter;
    },
  },
  methods: {
    groupLink(dance: TypeStats): string {
      return this.m4dLink(dance.groups![0].name);
    },
    styleLink(style: string): string {
      return this.m4dLink(wordsToKebab(style));
    },
    m4dLink(item: string): string {
      return `https://www.music4dance.net/dances/${item}`;
    },
    tempoLink(dance: TypeStats): string {
      const tempoRange = this.filteredTempo(dance);
      const numerator = dance.meter.numerator;
      return (
        "https://www.music4dance.net/song/?&filter=Index-" +
        `${dance.id}-Tempo-.-.-.-${tempoRange.min * numerator}-${
          tempoRange.max * numerator
        }`
      );
    },
    doFilter(item: TypeStats, filter: DanceFilter): boolean {
      return item.match(filter);
    },
    onFiltered(items: TypeStats[], length: number) {
      this.emptyTable =
        length === 0
          ? "Please select at least one item from every drop-down"
          : "";
    },
    filteredTempo(dance: TypeStats): TempoRange {
      const range = dance.filteredTempo(this.styles, this.organizations);
      if (range) {
        return range;
      } else {
        throw new Error(`Could not filter ${dance.name} with ${this.styles}`);
      }
    },
  },
});
</script>
