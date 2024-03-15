<script setup lang="ts">
import PageFrame from "@/components/PageFrame.vue";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { TempoType } from "@/models/DanceDatabase/TempoType";
import { computed, ref } from "vue";
import TempoCounter from "./components/TempoCounter.vue";
import DanceList from "./components/DanceList.vue";
import type { CountMethod } from "./CountMethod";

interface TempoModel {
  numerator?: number;
  tempo?: number;
  count?: CountMethod;
}

declare const model_: TempoModel;

const danceDatabase = safeDanceDatabase();
const beatsPerMeasure = ref(model_.numerator ?? 4);
const beatsPerMinute = ref(model_.tempo ?? 0);
const countMethod = ref<CountMethod>(model_.count ?? "beats");
const epsilonPercent = ref(5);
const dances = danceDatabase.dances;

const tempoType = computed(() =>
  countMethod.value === "measures" ? TempoType.Measures : TempoType.Beats,
);

const measuresPerMinute = computed<number>({
  get() {
    return beatsPerMinute.value / beatsPerMeasure.value;
  },
  set(value: number) {
    beatsPerMinute.value = value * beatsPerMeasure.value;
  },
});

function chooseDance(danceId: string): void {
  const dance = danceDatabase.fromId(danceId);
  if (dance) {
    window.open(`/dances/${dance.seoName}`, "_blank");
  }
}
</script>

<template>
  <PageFrame id="app">
    <TempoCounter
      v-model:beats-per-measure="beatsPerMeasure"
      v-model:beats-per-minute="beatsPerMinute"
      v-model:measures-per-minute="measuresPerMinute"
      v-model:count-method="countMethod"
      v-model:epsilon-percent="epsilonPercent"
    />
    <DanceList
      :dances="dances"
      :beats-per-measure="beatsPerMeasure"
      :beats-per-minute="beatsPerMinute"
      :tempo-type="tempoType"
      :epsilon-percent="epsilonPercent"
      :hide-name-link="true"
      @choose-dance="chooseDance"
    />
  </PageFrame>
</template>
./components/TempoCounter.vue
