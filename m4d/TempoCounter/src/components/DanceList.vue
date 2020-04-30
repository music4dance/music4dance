<template>
  <div>
    <b-list-group v-for='ds in dances' v-bind:key='ds.danceId'>
      <dance-item :dance='ds'></dance-item>
    </b-list-group>
  </div>
</template>

<script lang='ts'>
import { DanceStats } from '../model/DanceStats';
import { dancesForTempo, DanceOrder } from '../model/DanceManager';
import { Component, Vue } from 'vue-property-decorator';
import { Getter } from 'vuex-class';
import DanceItem from './DanceItem.vue';

@Component({
  components: {
    DanceItem,
  },
})
export default class DanceList extends Vue {
  @Getter private beatsPerMinute!: number;
  @Getter private beatsPerMeasure!: number;
  @Getter private epsilonPercent!: number;

  private get dances(): DanceOrder[] {
    return dancesForTempo(this.beatsPerMinute, this.beatsPerMeasure, this.epsilonPercent);
  }
}
</script>

<style scoped lang='scss'>
</style>