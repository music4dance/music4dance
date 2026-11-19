<script setup lang="ts">
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { TempoType } from "@/models/DanceDatabase/TempoType";
import { arraysEqualAsSets, useUrlQuerySync } from "@/composables/useUrlQuerySync";
import type { CheckboxOption } from "bootstrap-vue-next";
import { computed, ref } from "vue";
import type { CountMethod } from "./CountMethod";
import {
  validateCountMethod,
  validateEpsilon,
  validateNumerator,
  validateTempo,
} from "./QueryValidation";
import {
  type DeltaMetric,
  defaultDeltaMetrics,
  deltaMetrics,
  validateDeltaMetrics,
} from "./deltaMetrics";

interface TempoModel {
  numerator?: number;
  tempo?: number;
  count?: string;
  epsilon?: number;
  show?: string[];
}

declare const model_: TempoModel;

const danceDatabase = safeDanceDatabase();
const beatsPerMeasure = ref(validateNumerator(model_.numerator));
const beatsPerMinute = ref(validateTempo(model_.tempo));
const countMethod = ref<CountMethod>(validateCountMethod(model_.count));
const epsilonPercent = ref(validateEpsilon(model_.epsilon));
const metrics = ref<DeltaMetric[]>(validateDeltaMetrics(model_.show));
const dances = danceDatabase.dances;

const metricOptions: CheckboxOption[] = deltaMetrics.map((m) => ({ text: m.label, value: m.key }));
const showAdjustmentNote = computed(
  () => metrics.value.includes("pitch") || metrics.value.includes("speed"),
);

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

// Keeps the address bar (and therefore CopyLinkButton's default target) live as a shareable link
// to the counter's current configuration - see "Shareable URLs" in
// architecture/pages/tempo-counter-page.md.
useUrlQuerySync(() => ({
  numerator: beatsPerMeasure.value.toString(),
  tempo: beatsPerMinute.value.toFixed(1),
  count: countMethod.value,
  epsilon: epsilonPercent.value.toString(),
  // Omitted at its default, like Tempo List's `columns`, to keep the common case's URL short.
  show: arraysEqualAsSets(metrics.value, defaultDeltaMetrics) ? undefined : metrics.value,
}));

// Exposed for testing
defineExpose({
  beatsPerMeasure,
  beatsPerMinute,
  countMethod,
  epsilonPercent,
  metrics,
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
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 my-2">
      <div class="d-flex align-items-center gap-2">
        <span class="text-muted small">Show:</span>
        <CheckedList
          v-model="metrics"
          type="Detail"
          :options="metricOptions"
          variant="outline-secondary"
          size="sm"
        />
      </div>
      <CopyLinkButton label="Copy Link to This Tempo" />
    </div>
    <DanceDeltas
      :dances="dances"
      :beats-per-measure="beatsPerMeasure"
      :beats-per-minute="beatsPerMinute"
      :tempo-type="tempoType"
      :epsilon-percent="epsilonPercent"
      :hide-name-link="true"
      :metrics="metrics"
      @choose-dance="chooseDance"
    />
    <p v-if="showAdjustmentNote" class="text-muted small mt-2">
      To play a song at a dance's tempo, set <strong>Pitch</strong> on a CD player's or DJ deck's
      pitch (tempo) control: &minus; slows the song down, + speeds it up. Set
      <strong>Speed</strong> as the playback rate in tempo-change software: 100% is the original
      tempo.
    </p>
  </PageFrame>
</template>
