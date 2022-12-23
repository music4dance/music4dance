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
import Vue from "vue";
import TempoModal from "./TempoModal.vue";

export default Vue.extend({
  components: { TempoModal },
  props: { beatsPerMinute: { type: Number, required: true } },
});
</script>
