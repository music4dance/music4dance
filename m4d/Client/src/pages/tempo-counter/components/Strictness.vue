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
import { Component, Prop, Vue } from "vue-property-decorator";

@Component
export default class MeasuresPerMinute extends Vue {
  @Prop() private epsilonPercent!: number;

  private get value(): string {
    return this.epsilonPercent.toString();
  }

  private set value(v: string) {
    this.$emit("change-strictness", Number(v));
  }
}
</script>
