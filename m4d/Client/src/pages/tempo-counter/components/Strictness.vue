<template>
  <div style="margin-bottom: 1rem; width: 250px" class="mx-auto">
    <b-form-input
      id="strictness"
      v-model="value"
      type="range"
      min="0"
      max="20"
      step="0.5"
    ></b-form-input>
    <label for="strictness" class="d-flex justify-content-between"
      ><span>less</span><span class="ml-auto">more</span></label
    >
  </div>
</template>

<script lang="ts">
import { Component, Vue } from "vue-property-decorator";
import { Getter, Mutation } from "vuex-class";

@Component
export default class MeasuresPerMinute extends Vue {
  @Getter private epsilonPercent!: number;
  @Mutation private updateEpsilonPercent!: (value: number) => void;

  private get value(): string {
    return this.epsilonPercent.toString();
  }

  private set value(v: string) {
    this.updateEpsilonPercent(Number(v));
  }
}
</script>

<style scoped lang="scss"></style>
