<template>
  <span>
    <input
      v-if="editing && hasEditPermission"
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
import AdminTools from "@/mix-ins/AdminTools";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { SongProperty } from "@/model/SongProperty";
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";

@Component
export default class FieldEditor extends Mixins(
  EnvironmentManager,
  AdminTools
) {
  @Prop() private readonly name!: string;
  @Prop() private readonly value!: string;
  @Prop() private readonly editing!: boolean;
  @Prop() private readonly type?: string;
  @Prop() private readonly role?: string;
  @Prop() private readonly isCreator?: boolean;

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

  private get hasEditPermission(): boolean {
    const role = this.role;
    return !!this.isCreator || (!!role && this.hasRole(role));
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
