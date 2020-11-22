<template>
  <b-list-group-item
    :variant="variant"
    @click="chooseDance"
    class="d-flex justify-content-between align-items-center"
  >
    <span
      >{{ dance.name }} - <small>{{ meterDescription }}</small></span
    >
    <b-badge v-show="showDelta" :variant="variant">{{ deltaMessage }}</b-badge>
  </b-list-group-item>
</template>

<script lang="ts">
import { DanceOrder } from "@/model/DanceOrder";
import { Component, Vue, Prop } from "vue-property-decorator";
import { Getter } from "vuex-class";

@Component
export default class DanceItem extends Vue {
  @Prop() private readonly dance!: DanceOrder;
  @Getter private countMethod!: string;

  private get variant(): string {
    if (!this.showDelta) {
      return "primary";
    }

    return this.dance.mpmDelta < 0 ? "warning" : "success";
  }

  private get meterDescription() {
    return this.countMethod === "measures"
      ? this.dance.rangeMpmFormatted
      : this.dance.rangeBpmFormatted;
  }

  private get showDelta() {
    return Math.abs(this.dance.mpmDelta) >= 1.0;
  }

  private get deltaMessage(): string {
    const measures = this.countMethod === "measures";
    const slower = this.dance.bpmDelta < 0;
    const abs = Math.abs(
      measures ? this.dance.mpmDelta : this.dance.bpmDelta
    ).toFixed(1);
    return (
      abs + " " + (measures ? "M" : "B") + "PM " + (slower ? "slow" : "fast")
    );
  }

  private chooseDance(): void {
    window.open(
      `https://www.music4dance.net/dances/${this.dance.dance.seoName}`,
      "_blank"
    );
  }
}
</script>

<style scoped lang="scss"></style>
