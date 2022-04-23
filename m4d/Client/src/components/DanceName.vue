<template>
  <span
    ><span v-if="hideLink">{{ stats.name }}</span
    ><a v-else :href="danceLink">{{ stats.name }}</a>
    <span v-if="synonymText"
      ><br v-if="multiLine" />
      ({{ synonymText }})
    </span>
    <span v-if="showTempo && canShowTempo" style="font-size: 0.8rem">
      {{ tempoText }}</span
    >
  </span>
</template>

<script lang="ts">
import type { DanceStats } from "@/model/DanceStats";
import { TempoType } from "@/model/TempoType";
import { TypeStats } from "@/model/TypeStats";
import { Component, Prop, Vue } from "vue-property-decorator";

@Component
export default class DanceName extends Vue {
  @Prop() private dance!: DanceStats;
  @Prop({ default: TempoType.None }) private showTempo!: TempoType;
  @Prop() private showSynonyms?: boolean;
  @Prop() private multiLine?: boolean;
  @Prop() private hideLink?: boolean;

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

  private get canShowTempo(): boolean {
    const dance = this.stats.isGroup ? undefined : (this.stats as TypeStats);
    return !!dance && !dance.tempoRange.isInfinite;
  }

  private get tempoText(): string {
    const dance = this.stats.isGroup ? undefined : (this.stats as TypeStats);
    if (!dance) return "";

    const showTempo = this.showTempo;
    const bpm =
      showTempo & TempoType.Beats
        ? `${dance.tempoRange.bpm(dance.meter.numerator)} BPM`
        : "";
    const mpm =
      showTempo & TempoType.Measures
        ? `${dance.tempoRange.toString()} MPM`
        : "";

    return `${bpm}${mpm && bpm ? "/" : ""}${mpm}`;
  }

  private get synonymText(): string {
    const synonyms = this.dance.synonyms;
    return this.showSynonyms && synonyms ? `${synonyms.join(", ")}` : "";
  }
}
</script>
