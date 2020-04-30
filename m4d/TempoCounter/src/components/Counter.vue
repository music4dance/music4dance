<template>
  <div>
    <div class='row'>
      <div class='col-sm'><button class='btn btn-primary btn-lg btn-block' @click='countClicked'>{{counterTitle}}</button></div>
    </div>
    <div class='row my-2'></div>
    <div class='row'>
      <div class='col-sm'>
        <b-form-group>
          <b-form-radio-group
            id="countMethodInternal"
            v-model="countMethodInternal"
            :options="countOptions"
            buttons
            button-variant="outline-primary"
            size="md"
            name="radio-btn-outline">
          </b-form-radio-group>
        </b-form-group>
      </div>
      <div class='col-sm'>
        <beats-per-minute @change-tempo='changeBeatsPerMintue'/>
      </div>
      <div class='col-sm'>
        <measures-per-minute @change-tempo='changeMeasuresPerMintue'/>
      </div>
      <div class='col-sm'>
        <strictness/>
      </div>
    </div>
  </div>
</template>

<script lang='ts'>
import { Component, Prop, Watch, Vue } from 'vue-property-decorator';
import { Getter, Mutation } from 'vuex-class';
import BeatsPerMinute from './BeatsPerMinute.vue';
import MeasuresPerMinute from './MeasuresPerMinute.vue';
import Strictness from './Strictness.vue';

const maxWait = 3000;

const average = (list: number[]) =>
    list.length === 0 ? 0 :  list.reduce((prev, curr) => prev + curr) / list.length;

@Component({
  components: {
    BeatsPerMinute,
    MeasuresPerMinute,
    Strictness,
  },
})
export default class Counter extends Vue {
  // Data
  private intervals: number[] = [];  // deltas between last n clicked
  private last: number | null = null; // Last type clicked (in tics)
  private timeout: number | null = null;

  private countOptions: any = [
    { text: 'Count Measures', value: 'measures' },
    { text: 'Count Beats', value: 'beats' },
  ];

  // Getters
  @Getter private beatsPerMeasure!: number;
  @Getter private beatsPerMinute!: number;
  @Getter private measuresPerMinute!: number;
  @Getter private countMethod!: string;

  // Mutations
  @Mutation private updateBeatsPerMinute!: (value: number) => void;
  @Mutation private updateMeasuresPerMinute!: (value: number) => void;
  @Mutation private updateCountMethod!: (value: string) => void;

  // Computed
  private get countMeasures(): boolean {
    return this.countMethod === 'measures';
  }

  private get counterTitle(): string {
    return this.last ? 'Again' : this.counterInitialTitle;
  }

  private get counterInitialTitle(): string {
    return !this.countMeasures  ?
      'Click on each beat' :
      'Click on Downbeat of measure ' + this.beatsPerMeasure + '/4';
  }

  private get countMethodInternal(): string {
    return this.countMethod;
  }

  private set countMethodInternal(value: string) {
    this.updateCountMethod(value);
  }

  // Methods
  public countClicked() {
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
  }

  private changeBeatsPerMintue(newTempo: number) {
    this.updateBeatsPerMinute(newTempo);
    this.timerReset();
  }

  private changeMeasuresPerMintue(newTempo: number) {
    this.updateMeasuresPerMinute(newTempo);
    this.timerReset();
  }

  private timerReset(): void {
    if (this.timeout) {
      clearTimeout(this.timeout);
    }

    this.last = null;
  }

  private resetIntervals(): void {
    this.intervals.splice(0, this.intervals.length);
  }

  // Watchers
  @Watch('intervals')
  private onIntervalsUpdated() {
    const ms = average(this.intervals);
    const countsPerMinute =  ms ? (60 * 1000) / ms : 0;

    this.updateBeatsPerMinute(this.countMeasures ? countsPerMinute * this.beatsPerMeasure : countsPerMinute);
  }

  @Watch('countMethod')
  private onTypeChange() {
    this.timerReset();
  }
}
</script>

<style scoped lang='scss'>

</style>