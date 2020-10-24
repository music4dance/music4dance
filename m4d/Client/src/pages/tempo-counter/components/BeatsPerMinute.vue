<template>
  <div style="margin-bottom: 1rem">
    <b-button-group>
      <b-button variant="primary">BPM</b-button>
      <b-button variant="outline-primary" v-b-modal.bpm-modal>{{
        beatsPerMinute.toFixed(1)
      }}</b-button>
    </b-button-group>
    <tempo-modal
      identifier="bpm-modal"
      label="Beats Per Minute"
      :tempo="beatsPerMinute"
    ></tempo-modal>
  </div>
</template>

<script lang="ts">
import { Component, Vue } from "vue-property-decorator";
import { Getter, Mutation } from "vuex-class";
import TempoModal from "./TempoModal.vue";

@Component({
  components: {
    TempoModal,
  },
})
export default class BeatsPerMinute extends Vue {
  // Getters
  @Getter private beatsPerMinute!: number;
}
</script>

<style scoped lang="scss"></style>
