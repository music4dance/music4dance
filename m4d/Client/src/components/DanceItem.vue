<template>
  <div class="d-flex justify-content-between">
    <span
      ><a :href="danceLink">{{ stats.name }}</a>
      <span v-if="showTempo && canShowTempo" style="font-size: 0.8rem">
        ({{ tempoText }})</span
      >
    </span>
    <div>
      <b-badge :href="countLink" :variant="variant" style="line-height: 1.5"
        >songs ({{ stats.songCount }})</b-badge
      >
      <b-badge
        :href="danceLink"
        :variant="variant"
        style="line-height: 1.5"
        class="ml-2"
        >info</b-badge
      >
    </div>
  </div>
</template>

<script lang="ts">
import type { DanceStats } from "@/model/DanceStats";
import { TypeStats } from "@/model/TypeStats";
import { Component, Prop, Vue } from "vue-property-decorator";

@Component
export default class DanceItem extends Vue {
  @Prop() private dance!: DanceStats;
  @Prop() private showTempo?: boolean;

  private get stats(): DanceStats {
    if (!this.dance) {
      throw new Error("Dance not initialized yet");
    } else {
      return this.dance;
    }
  }

  private get danceLink(): string {
    return `/dances/${this.stats.seoName}`;
  }

  private get countLink(): string {
    return `/song/index?filter=.-OOX,${this.stats.id}-Dances`;
  }

  private get variant(): string {
    return this.stats.isGroup ? "secondary" : "primary";
  }

  private get style(): string {
    return this.stats.isGroup ? "color:white" : "";
  }

  private get canShowTempo(): boolean {
    return !this.stats.isGroup;
  }

  private get tempoText(): string {
    const dance = this.stats.isGroup ? undefined : (this.stats as TypeStats);
    return dance
      ? `${dance.tempoRange.bpm(
          dance.meter.numerator
        )} BPM/${dance.tempoRange.toString()} MPM`
      : "";
  }
}
</script>
