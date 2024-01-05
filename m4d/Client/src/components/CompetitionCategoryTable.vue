<template>
  <div>
    <h4 v-if="this.title">{{ this.title }}</h4>
    <b-table striped hover :items="dances" :fields="fields" responsive>
      <template v-slot:cell(name)="data">
        <a :href="danceLink(data.item)">{{ data.value }}</a>
      </template>
      <template v-slot:cell(mpm)="data">
        <a :href="defaultTempoLink(data.item)">{{ data.value }}</a>
      </template>
      <template v-slot:cell(dancesport)="data">
        <a :href="filteredTempoLink(data.item, 'dancesport')">{{
          data.value
        }}</a>
      </template>
      <template v-slot:cell(ndca)="data">
        <a :href="filteredTempoLink(data.item, 'ndca')">{{ data.value }}</a>
      </template>
      <template v-slot:cell(tempoRange)="data">
        <a :href="defaultTempoLink(data.item)">{{ data.value }}</a>
      </template>
    </b-table>
  </div>
</template>

<script lang="ts">
import { wordsToKebab } from "@/helpers/StringHelpers";
import { DanceInstance } from "@/model/DanceInstance";
import { Meter } from "@/model/Meter";
import { TempoRange } from "@/model/TempoRange";
import { BvTableFieldArray } from "bootstrap-vue";
import Vue, { PropType } from "vue";

export default Vue.extend({
  props: {
    dances: {
      type: [] as PropType<DanceInstance[]>,
      required: true,
    },
    title: {
      type: String,
      required: true,
    },
    useFullName: Boolean,
  },
  computed: {
    fields(): BvTableFieldArray {
      return [
        {
          key: "name",
          formatter: (value: string, key: string, item: DanceInstance) =>
            this.name(item),
        },
        {
          key: "mpm",
          label: "MPM",
          formatter: (value: null, key: string, item: DanceInstance) =>
            item.tempoRange.mpm(item.meter.numerator),
        },
        {
          key: "dancesport",
          label: "DanceSport",
          formatter: (value: null, key: string, item: DanceInstance) =>
            item.filteredTempo(["dancesport"])!.mpm(item.meter.numerator),
        },
        {
          key: "ndca",
          label: "NDCA",
          formatter: (value: null, key: string, item: DanceInstance) =>
            item.filteredTempo(["ndca"])!.mpm(item.meter.numerator),
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
      ];
    },
  },
  methods: {
    danceLink(dance: DanceInstance): string {
      return wordsToKebab(dance.shortName);
    },
    defaultTempoLink(dance: DanceInstance): string {
      return this.tempoLink(dance, dance.tempoRange);
    },
    filteredTempoLink(dance: DanceInstance, filter: string): string {
      return this.tempoLink(dance, dance.filteredTempo([filter])!);
    },
    tempoLink(dance: DanceInstance, tempo: TempoRange): string {
      return `/song/advancedsearch?dances=${dance.baseId}&tempomin=${tempo.min}&tempomax=${tempo.max}`;
    },
    name(dance: DanceInstance): string {
      return this.useFullName ? dance.name : dance.shortName;
    },
  },
});
</script>
