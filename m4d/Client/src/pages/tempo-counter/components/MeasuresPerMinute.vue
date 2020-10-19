<template>
  <div style='margin-bottom: 1rem'>
    <b-button-group>
      <b-dropdown id='meter-selector' :text='meterDescription' variant='primary'>
        <b-dropdown-item v-show='beatsPerMeasure!==2' @click='updateBeatsPerMeasure(2)'>MPM 2/4</b-dropdown-item>
        <b-dropdown-item v-show='beatsPerMeasure!==3' @click='updateBeatsPerMeasure(3)'>MPM 3/4</b-dropdown-item>
        <b-dropdown-item v-show='beatsPerMeasure!==4' @click='updateBeatsPerMeasure(4)' >MPM 4/4</b-dropdown-item>
      </b-dropdown>
      <b-button variant='outline-primary' v-b-modal.mpm-modal>{{measuresPerMinute.toFixed(1)}}</b-button>
    </b-button-group>
    <tempo-modal identifier='mpm-modal' label='Measures Per Minute' :tempo='measuresPerMinute'></tempo-modal>
  </div>
</template>

<script lang='ts'>
import { Component, Vue } from 'vue-property-decorator';
import { Getter, Mutation } from 'vuex-class';
import TempoModal from './TempoModal.vue';

@Component({
  components: {
    TempoModal,
  },
})
export default class MeasuresPerMinute extends Vue {
  // Getters
  @Getter private measuresPerMinute!: number;
  @Getter private beatsPerMeasure!: number;
  @Mutation private updateBeatsPerMeasure!: (numerator: number) => void;

  get meterDescription() {
    return 'MPM (' + this.beatsPerMeasure + '/4)';
  }
}
</script>

<style scoped lang='scss'>
</style>