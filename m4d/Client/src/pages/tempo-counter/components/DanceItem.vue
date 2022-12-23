<template>
  <b-list-group-item
    :variant="variant"
    href="#"
    @click="$emit('choose-dance', dance.dance.id, $event.ctrlKey)"
    class="d-flex justify-content-between align-items-center"
  >
    <dance-name
      :dance="dance.dance"
      :showTempo="tempoType"
      :showSynonyms="true"
      :hideLink="hideLink"
    ></dance-name>
    <b-badge v-show="showDelta" :variant="variant">{{ deltaMessage }}</b-badge>
  </b-list-group-item>
</template>

<script lang="ts">
import DanceName from "@/components/DanceName.vue";
import { DanceOrder } from "@/model/DanceOrder";
import { TempoType } from "@/model/TempoType";
import Vue, { PropType } from "vue";

export default Vue.extend({
  components: { DanceName },
  props: {
    dance: { type: Object as PropType<DanceOrder>, required: true },
    tempoType: { type: Number, default: TempoType.None },
    hideLink: Boolean,
  },
  computed: {
    temp(): string {
      return this.dance.rangeMpmFormatted;
    },
    variant(): string {
      if (!this.showDelta) {
        return "primary";
      }

      return this.dance.mpmDelta < 0 ? "warning" : "success";
    },
    showDelta(): boolean {
      return Math.abs(this.dance.mpmDelta) >= 1.0;
    },
    meterDescription(): string {
      return this.tempoType === TempoType.Measures
        ? this.dance.rangeMpmFormatted
        : this.dance.rangeBpmFormatted;
    },
    deltaMessage(): string {
      const measures = this.tempoType === TempoType.Measures;
      const slower = this.dance.bpmDelta < 0;
      const abs = Math.abs(
        measures ? this.dance.mpmDelta : this.dance.bpmDelta
      ).toFixed(1);
      return (
        abs + " " + (measures ? "M" : "B") + "PM " + (slower ? "slow" : "fast")
      );
    },
  },
  methods: {},
});
</script>
