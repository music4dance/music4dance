<template>
  <b-list-group :flush="flush">
    <b-list-group-item
      v-for="(dance, idx) in dances"
      :key="idx"
      :variant="danceVariant(dance)"
    >
      <dance-item :dance="dance" :showTempo="showTempo"></dance-item>
    </b-list-group-item>
  </b-list-group>
</template>

<script lang="ts">
import DanceItem from "@/components/DanceItem.vue";
import { DanceStats } from "@/model/DanceStats";
import { TempoType } from "@/model/TempoType";
import Vue, { PropType } from "vue";

export default Vue.extend({
  components: { DanceItem },
  props: {
    dances: { type: Array as PropType<DanceStats[]>, required: true },
    showTempo: { type: Number, default: TempoType.None },
    flush: Boolean,
    showSynonyms: Boolean,
  },
  computed: {
    filteredDances(): DanceStats[] {
      return this.dances.filter((d) => d.songCount > 0);
    },
  },
  methods: {
    danceVariant(dance: DanceStats): string {
      return dance.isGroup ? "primary" : "light";
    },
  },
});
</script>
