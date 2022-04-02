<template>
  <div>
    <b-list-group v-for="ds in orderedDances" v-bind:key="ds.danceId">
      <dance-item
        :dance="ds"
        :tempoType="tempoType"
        v-on="$listeners"
      ></dance-item>
    </b-list-group>
  </div>
</template>

<script lang="ts">
import { DanceOrder } from "@/model/DanceOrder";
import { TempoType } from "@/model/TempoType";
import { TypeStats } from "@/model/TypeStats";
import { Component, Prop, Vue } from "vue-property-decorator";
import DanceItem from "./DanceItem.vue";

@Component({ components: { DanceItem } })
export default class DanceList extends Vue {
  @Prop() private dances!: TypeStats[];
  @Prop() private beatsPerMinute!: number;
  @Prop() private beatsPerMeasure!: number;
  @Prop() private epsilonPercent!: number;
  @Prop() private tempoType!: TempoType;
  @Prop() private filter?: string;

  private get orderedDances(): DanceOrder[] {
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
  }
}
</script>
