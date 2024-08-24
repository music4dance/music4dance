<script setup lang="ts">
import { computed, ref, watch, watchEffect } from "vue";
import type { CountMethod } from "../CountMethod";

/// The states for the core state machine
///  - Initial = no data (initial state or timer reset without getting past firstClick state)
///  - FirstClick = the user has clicked once after inital/done state
///  - Counting = second - infinite continuous clicking without ever pausing for _maxTime
///  - Done = user has paused for maxWait after clicking at least twice
enum ClickState {
  Initial,
  FirstClick,
  Counting,
  Done,
}

const maxWait = 5000;

const average = (list: number[]) =>
  list.length === 0 ? 0 : list.reduce((prev, curr) => prev + curr) / list.length;

let timer: ReturnType<typeof setTimeout> | null = null;

const beatsPerMeasure = defineModel<number>("beatsPerMeasure", { required: true });
const beatsPerMinute = defineModel<number>("beatsPerMinute", { required: true });
const measuresPerMinute = defineModel<number>("measuresPerMinute", { required: true });
const epsilonPercent = defineModel<number>("epsilonPercent", { required: true });
const countMethod = defineModel<CountMethod>("countMethod", { required: true });

const intervals = ref<number[]>([]); // deltas between last n clicked
const last = ref<number | null>(null); // Last type clicked (in tics)
const state = ref<ClickState>(beatsPerMeasure.value ? ClickState.Done : ClickState.Initial);

const countOptions = [
  { text: "Count Beats", value: "beats" },
  { text: "Count Measures", value: "measures" },
];

const counterTitle = computed(() => {
  return last.value
    ? "Again"
    : countMeasures.value
      ? "Click on each " + beatsPerMeasure.value + "/4 measure"
      : "Click on each beat";
});

const countMeasures = computed(() => {
  return countMethod.value === "measures";
});

watchEffect(() => {
  if (state.value === ClickState.Done && intervals.value.length === 0) {
    // This is the case where a tempo is passed in
    beatsPerMinute.value = measuresPerMinute.value * beatsPerMeasure.value;
  } else {
    // And this is the normal case where the user is clicking
    const ms = average(intervals.value);
    const countsPerMinute = ms ? (60 * 1000) / ms : 0;

    beatsPerMinute.value = countMeasures.value
      ? countsPerMinute * beatsPerMeasure.value
      : countsPerMinute;
  }
});

watch(countMethod, () => {
  timerReset();
});

function countClicked(): void {
  const current = new Date().getTime();
  if (timer) {
    clearTimeout(timer);
  }

  switch (state.value) {
    case ClickState.Initial:
    case ClickState.Done:
      intervals.value = [];
      state.value = ClickState.Counting;
      break;
    case ClickState.FirstClick:
    case ClickState.Counting:
      {
        state.value = ClickState.Counting;
        const delta = current - last.value!;
        intervals.value =
          intervals.value.length >= 10
            ? [...intervals.value.slice(1), delta]
            : [...intervals.value, delta];
      }
      break;
  }
  last.value = current;

  timer = setTimeout(timerReset, maxWait);
}

function timerReset(): void {
  if (timer) {
    clearTimeout(timer);
  }
  switch (state.value) {
    case ClickState.Initial:
    case ClickState.FirstClick:
      state.value = ClickState.Initial;
      intervals.value = [];
      break;
    case ClickState.Counting:
    case ClickState.Done:
      state.value = ClickState.Done;
      break;
  }

  last.value = null;
}
</script>

<template>
  <div>
    <h1>Tempo Counter</h1>
    <div class="row my-2 mx-2">
      <!-- INT-TODO: Block buttons not working correctly? -->
      <BButton variant="primary" size="lg" class="col counter-button" @click="countClicked">
        {{ counterTitle }}
      </BButton>
    </div>
    <div class="row">
      <BFormGroup class="col-sm-4" style="margin-bottom: 1rem">
        <BFormRadioGroup
          id="countMethodInternal"
          v-model="countMethod"
          :options="countOptions"
          buttons
          button-variant="outline-primary"
          size="md"
          name="radio-btn-outline"
        />
      </BFormGroup>
      <BeatsPerMinute v-model="beatsPerMinute" class="col-sm" @update:model-value="timerReset" />
      <MeasuresPerMinute
        v-model="measuresPerMinute"
        v-model:beats-per-measure="beatsPerMeasure"
        class="col-sm"
        @update:model-value="timerReset"
      />
      <StrictSlider v-model="epsilonPercent" class="col-sm-3" />
    </div>
    <div class="row">
      <div class="col-sm-4">BPM: {{ beatsPerMinute.toFixed(1) }}</div>
      <div class="col-sm-4">MPM: {{ measuresPerMinute.toFixed(1) }}</div>
      <div class="col-sm-4">METER: {{ beatsPerMeasure }}/4</div>
    </div>
  </div>
</template>

<style scoped>
.counter-button {
  height: 3.5em;
  font-size: xx-large;
}
</style>
