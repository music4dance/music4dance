<template>
  <div class="d-flex justify-content-between">
    <dance-name
      :dance="dance"
      :showTempo="showTempo"
      :showSynonyms="showSynonyms"
    ></dance-name>
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
import { TempoType } from "@/model/TempoType";
import Vue, { PropType } from "vue";
import DanceName from "./DanceName.vue";

export default Vue.extend({
  components: { DanceName },
  props: {
    dance: { type: Object as PropType<DanceStats>, required: true },
    showTempo: { type: Number, default: TempoType.None },
    showSynonyms: Boolean,
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
    countLink(): string {
      return `/song/index?filter=.-OOX,${this.stats.id}-Dances`;
    },
    variant(): string {
      return this.stats.isGroup ? "secondary" : "primary";
    },
    style(): string {
      return this.stats.isGroup ? "color:white" : "";
    },
  },
  methods: {},
});
</script>
