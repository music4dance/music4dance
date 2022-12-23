<template>
  <div>
    <div class="row">
      <div class="col-sm">
        <button class="btn btn-primary btn-lg btn-block" @click="countClicked">
          {{ counterTitle }}
        </button>
      </div>
    </div>
    <div class="row my-2"></div>
    <div class="row">
      <div class="col-sm">
        <b-form-group>
          <b-form-radio-group
            id="countMethodInternal"
            v-model="countMethodInternal"
            :options="countOptions"
            buttons
            button-variant="outline-primary"
            size="md"
            name="radio-btn-outline"
          >
          </b-form-radio-group>
        </b-form-group>
      </div>
      <div class="col-sm">
        <beats-per-minute
          :beatsPerMinute="beatsPerMinute"
          @change-tempo="changeBeatsPerMintue"
        />
      </div>
      <div class="col-sm">
        <measures-per-minute
          :measuresPerMinute="measuresPerMinute"
          :beatsPerMeasure="beatsPerMeasure"
          @change-tempo="changeMeasuresPerMintue"
          @change-meter="changeBeatsPerMeasure"
        />
      </div>
      <div class="col-sm">
        <strictness
          :epsilonPercent="epsilonPercent"
          @change-strictness="$emit('update:epsilon-percent', $event)"
        />
      </div>
    </div>
  </div>
</template>

<script lang="ts">
import Vue from "vue";
import BeatsPerMinute from "./BeatsPerMinute.vue";
import MeasuresPerMinute from "./MeasuresPerMinute.vue";
import Strictness from "./Strictness.vue";

const maxWait = 5000;

const average = (list: number[]) =>
  list.length === 0
    ? 0
    : list.reduce((prev, curr) => prev + curr) / list.length;

export default Vue.extend({
  components: { BeatsPerMinute, MeasuresPerMinute, Strictness },
  props: {
    beatsPerMeasure: Number,
    beatsPerMinute: Number,
    measuresPerMinute: Number,
    countMethod: String,
    epsilonPercent: Number,
  },
  data() {
    return new (class {
      intervals: number[] = []; // deltas between last n clicked
      last: number | null = null; // Last type clicked (in tics)
      timeout: number | null = null;
      countOptions = [
        { text: "Count Measures", value: "measures" },
        { text: "Count Beats", value: "beats" },
      ];
    })();
  },
  computed: {
    counterTitle(): string {
      return this.last ? "Again" : this.counterInitialTitle;
    },
    counterInitialTitle(): string {
      return !this.countMeasures
        ? "Click on each beat"
        : "Click on Downbeat of measure " + this.beatsPerMeasure + "/4";
    },
    countMeasures(): boolean {
      return this.countMethod === "measures";
    },
    countMethodInternal: {
      get: function (): string {
        return this.countMethod;
      },
      set: function (value: string) {
        this.$emit("update:count-method", value);
      },
    },
  },
  watch: {
    intervals(): void {
      const ms = average(this.intervals);
      const countsPerMinute = ms ? (60 * 1000) / ms : 0;

      this.$emit(
        "update:beats-per-minute",
        this.countMeasures
          ? countsPerMinute * this.beatsPerMeasure
          : countsPerMinute
      );
    },
    countMethod(): void {
      this.timerReset();
    },
  },
  methods: {
    countClicked(): void {
      const current = new Date().getTime();
      if (this.timeout) {
        clearTimeout(this.timeout);
      }

      if (this.last == null) {
        this.resetIntervals();
      } else {
        const delta = current - this.last;
        if (delta > maxWait) {
          this.resetIntervals();
        } else {
          this.intervals.push(delta);
          if (this.intervals.length > 100) {
            this.intervals.shift();
          }
        }
      }
      this.last = current;

      this.timeout = setTimeout(this.timerReset, maxWait);
    },
    changeBeatsPerMintue(newTempo: number) {
      this.$emit("update:beats-per-minute", newTempo);
      this.timerReset();
    },
    changeMeasuresPerMintue(newTempo: number) {
      this.$emit("update:measures-per-minute", newTempo);
      this.timerReset();
    },
    changeBeatsPerMeasure(newTempo: number) {
      this.$emit("update:beats-per-measure", newTempo);
      this.timerReset();
    },
    timerReset(): void {
      if (this.timeout) {
        clearTimeout(this.timeout);
      }

      this.last = null;
    },
    resetIntervals(): void {
      this.intervals.splice(0, this.intervals.length);
    },
  },
});
</script>
