<template>
  <span>
    <input
      v-if="editting"
      v-model="internalValue"
      :type="computedType"
      class="form-control ml-2"
      style="display: inline"
      :class="{ number: isNumber, text: !isNumber }"
    />
    <span v-else
      ><slot>{{ value }}</slot></span
    >
  </span>
</template>

<script lang="ts">
import "reflect-metadata";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { Component, Mixins, Prop } from "vue-property-decorator";
import { SongProperty } from "@/model/SongProperty";

@Component
export default class FieldEditor extends Mixins(EnvironmentManager) {
  @Prop() private readonly name!: string;
  @Prop() private readonly value!: string;
  @Prop() private readonly editting!: boolean;
  @Prop() private readonly type?: string;

  private get internalValue(): string {
    return this.value;
  }

  private set internalValue(value: string) {
    this.$emit(
      "update-field",
      new SongProperty({ name: this.name, value: value })
    );
  }

  private get isNumber(): boolean {
    return this.type === "number";
  }

  private get computedType(): string {
    return this.type ?? "text";
  }
}
</script>

<style lang="scss" scoped>
.number {
  width: 5em;
}
.text {
  width: auto;
}
</style>
