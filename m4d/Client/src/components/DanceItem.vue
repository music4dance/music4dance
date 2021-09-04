<template>
  <div class="d-flex justify-content-between">
    <span
      ><a :href="danceLink" :style="safeStyle">{{ stats.name }}</a>
      <span v-if="showTempo && canShowTempo" style="font-size: 0.8rem">
        ({{ tempoText }})</span
      >
    </span>
    <b-badge
      :href="countLink"
      :variant="safeVariant"
      style="line-height: 1.5"
      >{{ stats.songCount }}</b-badge
    >
  </div>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import type { DanceStats, TypeStats } from "@/model/DanceStats";

@Component
export default class DanceItem extends Vue {
  @Prop() private dance!: DanceStats;
  @Prop() private variant?: string;
  @Prop() private textStyle?: string;
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

  private get safeVariant(): string {
    return this.variant ?? "primary";
  }

  private get safeStyle(): string {
    return this.textStyle ?? "";
  }

  private get canShowTempo(): boolean {
    return !this.stats.isGroup;
  }

  private get tempoText(): string {
    const dance = this.stats.isGroup ? undefined : (this.stats as TypeStats);
    return dance
      ? `${dance.tempoRange.toString()} BPM/${dance.tempoRange.mpm(
          dance.meter.numerator
        )} MPM`
      : "";
  }
}
</script>
