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
import mixins from "vue-typed-mixins";

export default mixins(EnvironmentManager, AdminTools).extend({
  props: {
    name: { type: String, required: true },
    value: { type: String, required: true },
    editing: Boolean,
    type: String,
    role: String,
    isCreator: Boolean,
  },
  data() {
    return new (class {})();
  },
  computed: {
    internalValue: {
      get: function (): string {
        return this.value;
      },
      set: function (value: string): void {
        this.$emit(
          "update-field",
          new SongProperty({ name: this.name, value: value })
        );
      },
    },
    isNumber(): boolean {
      return this.type === "number";
    },

    computedType(): string {
      return this.type ?? "text";
    },

    hasEditPermission(): boolean {
      const role = this.role;
      return !!this.isCreator || (!!role && this.hasRole(role));
    },
  },
});
</script>

<style lang="scss" scoped>
.number {
  width: 5em;
}
.text {
  width: auto;
}
</style>
