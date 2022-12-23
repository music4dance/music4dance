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
import Vue, { PropType } from "vue";

export default Vue.extend({
  components: {},
  props: {
    dance: { type: Object as PropType<DanceStats>, required: true },
    showTempo: { type: Number, default: TempoType.None },
    showSynonyms: Boolean,
    multiLine: Boolean,
    hideLink: Boolean,
  },
  computed: {
    stats(): DanceStats {
      if (!this.dance) {
        throw new Error("Dance not initialized yet");
      } else {
        return this.dance;
      }
    },

    danceLink(): string {
      return `/dances/${this.stats.seoName}`;
    },

    canShowTempo(): boolean {
      const dance = this.stats.isGroup ? undefined : (this.stats as TypeStats);
      return !!dance && !dance.tempoRange.isInfinite;
    },

    tempoText(): string {
      const dance = this.stats.isGroup ? undefined : (this.stats as TypeStats);
      if (!dance) return "";

      const showTempo = this.showTempo;
      const bpm =
        showTempo & TempoType.Beats ? `${dance.tempoRange.toString()} BPM` : "";
      const mpm =
        showTempo & TempoType.Measures
          ? `${dance.tempoRange.mpm(dance.meter.numerator)} MPM`
          : "";

      return `${bpm}${mpm && bpm ? "/" : ""}${mpm}`;
    },

    synonymText(): string {
      const synonyms = this.dance.synonyms;
      return this.showSynonyms && synonyms ? `${synonyms.join(", ")}` : "";
    },
  },
});
</script>
