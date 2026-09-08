<script setup lang="ts">
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { TempoType } from "@/models/DanceDatabase/TempoType";
import { computed, ref } from "vue";
import type { CountMethod } from "./CountMethod";
import {
  validateCountMethod,
  validateEpsilon,
  validateNumerator,
  validateTempo,
} from "./QueryValidation";

interface TempoModel {
  numerator?: number;
  tempo?: number;
  count?: string;
  epsilon?: number;
}

declare const model_: TempoModel;

const danceDatabase = safeDanceDatabase();
const beatsPerMeasure = ref(validateNumerator(model_.numerator));
const beatsPerMinute = ref(validateTempo(model_.tempo));
const countMethod = ref<CountMethod>(validateCountMethod(model_.count));
const epsilonPercent = ref(validateEpsilon(model_.epsilon));
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

// Exposed for testing
defineExpose({
  beatsPerMeasure,
  beatsPerMinute,
  countMethod,
  epsilonPercent,
  tempoType,
  measuresPerMinute,
  chooseDance,
});
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
    <DanceDeltas
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
