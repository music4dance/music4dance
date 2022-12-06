<template>
  <page id="app">
    <counter
      :beatsPerMeasure="beatsPerMeasure"
      :beatsPerMinute="beatsPerMinute"
      :measuresPerMinute="measuresPerMinute"
      :countMethod="countMethod"
      :epsilonPercent="epsilonPercent"
      @update:beats-per-measure="beatsPerMeasure = $event"
      @update:beats-per-minute="beatsPerMinute = $event"
      @update:measures-per-minute="measuresPerMinute = $event"
      @update:count-method="countMethod = $event"
      @update:epsilon-percent="epsilonPercent = $event"
    />
    <dance-list
      :dances="dances"
      :beatsPerMeasure="beatsPerMeasure"
      :beatsPerMinute="beatsPerMinute"
      :tempoType="tempoType"
      :epsilonPercent="epsilonPercent"
      @choose-dance="chooseDance"
    />
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import { safeEnvironment } from "@/helpers/DanceEnvironmentManager";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import { TempoType } from "@/model/TempoType";
import { TypeStats } from "@/model/TypeStats";
import Vue from "vue";
import Counter from "./components/Counter.vue";
import DanceList from "./components/DanceList.vue";

interface TempoModel {
  numerator?: number;
  tempo?: number;
}

declare const model: TempoModel;

export default Vue.extend({
  components: { Counter, DanceList, Page },
  props: {},
  data() {
    return new (class {
      environment?: DanceEnvironment = safeEnvironment();
      beatsPerMeasure = model.numerator ?? 4;
      beatsPerMinute = model.tempo ?? 0;
      countMethod = "measures";
      epsilonPercent = 5;
    })();
  },
  computed: {
    dances(): TypeStats[] {
      const environment = this.environment;
      return environment ? environment.dances! : [];
    },
    tempoType(): TempoType {
      return this.countMethod === "measures"
        ? TempoType.Measures
        : TempoType.Beats;
    },
    measuresPerMinute: {
      get: function (): number {
        return this.beatsPerMinute / this.beatsPerMeasure;
      },
      set: function (value: number) {
        this.beatsPerMinute = value * this.beatsPerMeasure;
      },
    },
  },
  methods: {
    chooseDance(danceId: string): void {
      const dance = this.environment!.fromId(danceId);
      if (dance) {
        window.open(`/dances/${dance.seoName}`, "_blank");
      }
    },
  },
});
</script>
