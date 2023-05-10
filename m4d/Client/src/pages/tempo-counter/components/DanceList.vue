<template>
  <div>
    <b-list-group v-for="ds in orderedDances" v-bind:key="ds.dance.id">
      <dance-item
        :dance="ds"
        :tempoType="tempoType"
        :hideLink="hideNameLink"
        v-on="$listeners"
      ></dance-item>
    </b-list-group>
  </div>
</template>

<script lang="ts">
import { DanceOrder } from "@/model/DanceOrder";
import { TypeStats } from "@/model/TypeStats";
import Vue, { PropType } from "vue";
import DanceItem from "./DanceItem.vue";

export default Vue.extend({
  components: { DanceItem },
  props: {
    dances: { type: Array as PropType<TypeStats[]>, required: true },
    beatsPerMinute: Number,
    beatsPerMeasure: Number,
    epsilonPercent: Number,
    tempoType: Number,
    filter: String,
    hideNameLink: Boolean,
  },
  computed: {
    orderedDances(): DanceOrder[] {
      const filter = this.filter;
      const dances =
        this.dances.length > 0
          ? DanceOrder.dancesForTempo(
              this.dances,
              this.beatsPerMinute,
              this.beatsPerMeasure,
              this.epsilonPercent
            )
          : [];
      return filter
        ? dances.filter((d) => d.name.toLowerCase().indexOf(filter) !== -1)
        : dances;
    },
  },
});
</script>
