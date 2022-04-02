<template>
  <b-list-group-item
    :variant="variant"
    href="#"
    @click="$emit('choose-dance', dance.dance.id, $event.ctrlKey)"
    class="d-flex justify-content-between align-items-center"
  >
    <dance-name
      :dance="dance.dance"
      :tempoType="tempoType"
      :showTempo="true"
      :showSynonyms="true"
    ></dance-name>
    <b-badge v-show="showDelta" :variant="variant">{{ deltaMessage }}</b-badge>
  </b-list-group-item>
</template>

<script lang="ts">
import DanceName from "@/components/DanceName.vue";
import { DanceOrder } from "@/model/DanceOrder";
import { TempoType } from "@/model/TempoType";
import { Component, Prop, Vue } from "vue-property-decorator";

@Component({ components: { DanceName } })
export default class DanceItem extends Vue {
  @Prop() private readonly dance!: DanceOrder;
  @Prop() private tempoType!: TempoType;

  private get variant(): string {
    if (!this.showDelta) {
      return "primary";
    }

    return this.dance.mpmDelta < 0 ? "warning" : "success";
  }

  private get meterDescription() {
    return this.tempoType === TempoType.Measures
      ? this.dance.rangeMpmFormatted
      : this.dance.rangeBpmFormatted;
  }

  private get showDelta() {
    return Math.abs(this.dance.mpmDelta) >= 1.0;
  }

  private get deltaMessage(): string {
    const measures = this.tempoType === TempoType.Measures;
    const slower = this.dance.bpmDelta < 0;
    const abs = Math.abs(
      measures ? this.dance.mpmDelta : this.dance.bpmDelta
    ).toFixed(1);
    return (
      abs + " " + (measures ? "M" : "B") + "PM " + (slower ? "slow" : "fast")
    );
  }
}
</script>
