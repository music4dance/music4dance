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
      <template v-slot:cell(ndca-1)="data">
        <a :href="filteredTempoLink(data.item, 'ndca-1')">{{ data.value }}</a>
      </template>
      <template v-slot:cell(ndca-2)="data">
        <a :href="filteredTempoLink(data.item, 'ndca-2')">{{ data.value }}</a>
      </template>
      <template v-slot:cell(tempoRange)="data">
        <a :href="defaultTempoLink(data.item)">{{ data.value }}</a>
      </template>
    </b-table>
    <p v-if="isMixed">
      (*) A short explanation of the NDCA (<a
        href="https://www.ndca.org/pages/ndca_rule_book/Default.asp"
        >National Dance Council of America)</a
      >
      columns: For American style dances the "A" column contains Tempi for
      Silver and Gold levels while the "B" column contains Tempi for Bronze
      level. For International style dances the "A" column contains Tempi for
      Professional and Amateur couples while the "B" column contains Tempi for
      Pro/Am couples.
    </p>
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
    useFullName: String,
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
          key: "ndca-1",
          label: this.ndcaATitle,
          formatter: (value: null, key: string, item: DanceInstance) =>
            item.filteredTempo(["ndca-1"])!.mpm(item.meter.numerator),
        },
        {
          key: "ndca-2",
          label: this.ndcaBTitle,
          formatter: (value: null, key: string, item: DanceInstance) =>
            item.filteredTempo(["ndca-2"])!.mpm(item.meter.numerator),
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
    ndcaATitle(): string {
      const family = this.styleFamily;
      switch (family) {
        case "American":
          return "NDCA Silver/Gold";
        case "International":
          return "NDCA Professional or Amateur";
        default:
          return "NDCA A(*)";
      }
    },
    ndcaBTitle(): string {
      const family = this.styleFamily;
      switch (family) {
        case "American":
          return "NDCA Bronze";
        case "International":
          return "NDCA Pro/Am";
        default:
          return "NDCA B(*)";
      }
    },
    styleFamily(): string {
      if (!this.dances || this.dances.length === 0) {
        return "both";
      }
      const family = this.dances[0].styleFamily;
      return this.dances.every((d) => d.styleFamily === family)
        ? family
        : "Both";
    },
    isMixed(): boolean {
      return this.styleFamily === "Both";
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
