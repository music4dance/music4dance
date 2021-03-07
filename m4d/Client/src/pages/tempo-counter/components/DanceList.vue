<template>
  <div>
    <b-list-group v-for="ds in orderedDances" v-bind:key="ds.danceId">
      <dance-item :dance="ds" :countMethod="countMethod"></dance-item>
    </b-list-group>
  </div>
</template>

<script lang="ts">
import { DanceOrder } from "@/model/DanceOrder";
import { DanceStats } from "@/model/DanceStats";
import { Component, Vue, Prop } from "vue-property-decorator";
import DanceItem from "./DanceItem.vue";

@Component({
  components: {
    DanceItem,
  },
})
export default class DanceList extends Vue {
  @Prop() private dances!: DanceStats[];
  @Prop() private beatsPerMinute!: number;
  @Prop() private beatsPerMeasure!: number;
  @Prop() private epsilonPercent!: number;
  @Prop() private countMethod!: string;

  private get orderedDances(): DanceOrder[] {
    return DanceOrder.dancesForTempo(
      this.dances,
      this.beatsPerMinute,
      this.beatsPerMeasure,
      this.epsilonPercent
    );
  }
}
</script>

<style scoped lang="scss"></style>
