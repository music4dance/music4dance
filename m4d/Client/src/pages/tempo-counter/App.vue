<template>
  <page
    id="app"
    :consumesEnvironment="true"
    @environment-loaded="onEnvironmentLoaded"
  >
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
      :dances="dances()"
      :beatsPerMeasure="beatsPerMeasure"
      :beatsPerMinute="beatsPerMinute"
      :countMethod="countMethod"
      :epsilonPercent="epsilonPercent"
      @choose-dance="chooseDance"
    />
  </page>
</template>

<script lang="ts">
import { Component, Vue } from "vue-property-decorator";
import Page from "@/components/Page.vue";
import Counter from "./components/Counter.vue";
import DanceList from "./components/DanceList.vue";
import { TypeStats } from "@/model/DanceStats";
import { DanceEnvironment } from "@/model/DanceEnvironmet";

@Component({
  components: {
    Counter,
    DanceList,
    Page,
  },
})
export default class App extends Vue {
  private environment?: DanceEnvironment;
  public beatsPerMeasure = 4;
  public beatsPerMinute = 0;
  public countMethod = "measures";
  public epsilonPercent = 5;

  constructor() {
    super();

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const beatsPerMeasure = (window as any).initialNumerator;
    if (beatsPerMeasure) {
      this.beatsPerMeasure = beatsPerMeasure;
    }

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const beatsPerMinute = (window as any).initialTempo;
    if (beatsPerMinute) {
      this.beatsPerMinute = beatsPerMinute;
    }
  }

  private chooseDance(danceId: string): void {
    const dance = this.environment!.fromId(danceId);
    if (dance) {
      window.open(`/dances/${dance.seoName}`, "_blank");
    }
  }

  private dances(): TypeStats[] {
    const environment = this.environment;
    return environment ? environment.dances! : [];
  }

  private get measuresPerMinute(): number {
    return this.beatsPerMinute / this.beatsPerMeasure;
  }

  private set measuresPerMinute(value: number) {
    this.beatsPerMinute = value * this.beatsPerMeasure;
  }

  private onEnvironmentLoaded(environment: DanceEnvironment): void {
    this.environment = environment;
  }
}
</script>
