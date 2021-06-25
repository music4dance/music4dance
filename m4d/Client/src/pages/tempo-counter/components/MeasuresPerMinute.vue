<template>
  <div style="margin-bottom: 1rem">
    <b-button-group>
      <b-dropdown
        id="meter-selector"
        :text="meterDescription"
        variant="primary"
      >
        <b-dropdown-item
          v-show="beatsPerMeasure !== 2"
          @click="$emit('change-meter', 2)"
          >MPM 2/4</b-dropdown-item
        >
        <b-dropdown-item
          v-show="beatsPerMeasure !== 3"
          @click="$emit('change-meter', 3)"
          >MPM 3/4</b-dropdown-item
        >
        <b-dropdown-item
          v-show="beatsPerMeasure !== 4"
          @click="$emit('change-meter', 4)"
          >MPM 4/4</b-dropdown-item
        >
      </b-dropdown>
      <b-button variant="outline-primary" v-b-modal.mpm-modal>{{
        measuresPerMinute.toFixed(1)
      }}</b-button>
    </b-button-group>
    <tempo-modal
      identifier="mpm-modal"
      label="Measures Per Minute"
      :tempo="measuresPerMinute"
    ></tempo-modal>
  </div>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import TempoModal from "./TempoModal.vue";

@Component({
  components: {
    TempoModal,
  },
})
export default class MeasuresPerMinute extends Vue {
  @Prop() private measuresPerMinute!: number;
  @Prop() private beatsPerMeasure!: number;

  get meterDescription(): string {
    return "MPM (" + this.beatsPerMeasure + "/4)";
  }
}
</script>
