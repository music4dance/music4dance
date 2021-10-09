<template>
  <div>
    <h4 v-if="this.title">{{ this.title }}</h4>
    <b-table striped hover :items="dances" :fields="fields" responsive>
      <template v-slot:cell(name)="data">
        <a :href="danceLink(data.item)">{{ data.value }}</a>
      </template>
      <template v-slot:cell(tempoRange)="data">
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
      <template v-slot:cell(bpm)="data">
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
import { Component, Prop, Vue } from "vue-property-decorator";

@Component
export default class CompetitionCategoryTable extends Vue {
  @Prop() private dances!: DanceInstance[];
  @Prop() private title!: string;
  @Prop() private useFullName?: boolean;

  private fields: BvTableFieldArray = [
    {
      key: "name",
      formatter: (value: string, key: string, item: DanceInstance) =>
        this.name(item),
    },
    {
      key: "tempoRange",
      label: "MPM",
      formatter: (value: TempoRange) => value.toString(),
    },
    {
      key: "dancesport",
      label: "DanceSport",
      formatter: (value: null, key: string, item: DanceInstance) =>
        item.filteredTempo(["dancesport"])!.toString(),
    },
    {
      key: "ndca-1",
      label: this.ndcaATitle,
      formatter: (value: null, key: string, item: DanceInstance) =>
        item.filteredTempo(["ndca-1"])!.toString(),
    },
    {
      key: "ndca-2",
      label: this.ndcaBTitle,
      formatter: (value: null, key: string, item: DanceInstance) =>
        item.filteredTempo(["ndca-2"])!.toString(),
    },
    {
      key: "bpm",
      label: "BPM",
      formatter: (value: null, key: string, item: DanceInstance) =>
        item.tempoRange.bpm(item.meter.numerator),
    },
    {
      key: "meter",
      formatter: (value: Meter) => value.toString(),
    },
  ];

  private get ndcaATitle(): string {
    const family = this.styleFamily;
    switch (family) {
      case "American":
        return "NDCA Silver/Gold";
      case "International":
        return "NDCA Professional or Amateur";
      default:
        return "NDCA A(*)";
    }
  }

  private get ndcaBTitle(): string {
    const family = this.styleFamily;
    switch (family) {
      case "American":
        return "NDCA Bronze";
      case "International":
        return "NDCA Pro/Am";
      default:
        return "NDCA B(*)";
    }
  }

  private get styleFamily(): string {
    if (!this.dances || this.dances.length === 0) {
      return "both";
    }
    const family = this.dances[0].styleFamily;
    return this.dances.every((d) => d.styleFamily === family) ? family : "Both";
  }

  private get isMixed(): boolean {
    return this.styleFamily === "Both";
  }

  private danceLink(dance: DanceInstance): string {
    return wordsToKebab(dance.shortName);
  }

  private defaultTempoLink(dance: DanceInstance): string {
    return this.tempoLink(dance, dance.tempoRange.toBpm(dance.meter.numerator));
  }

  private filteredTempoLink(dance: DanceInstance, filter: string): string {
    return this.tempoLink(
      dance,
      dance.filteredTempo([filter])!.toBpm(dance.meter.numerator)
    );
  }

  private tempoLink(dance: DanceInstance, tempo: TempoRange): string {
    return `/song/advancedsearch?dances=${dance.baseId}&tempomin=${tempo.min}&tempomax=${tempo.max}`;
  }

  private name(dance: DanceInstance): string {
    return this.useFullName ? dance.name : dance.shortName;
  }
}
</script>
