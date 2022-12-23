<template>
  <div>
    <b-button
      v-if="canAdd"
      variant="outline-primary"
      size="sm"
      block
      style="text-align: left"
      class="mb-2"
      @click="onAdd"
    >
      <b-icon-lock-fill aria-label="add"></b-icon-lock-fill>
      {{ friendlyName }}: <b>{{ value }}</b>
    </b-button>
    <div v-else>
      {{ friendlyName }}: <b>{{ value }}</b>
    </div>
  </div>
</template>

<script lang="ts">
import { camelToPascal } from "@/helpers/StringHelpers";
import "reflect-metadata";
import Vue from "vue";

export default Vue.extend({
  props: {
    name: { type: String, required: true },
    value: { type: String, required: true },
    canAdd: Boolean,
  },
  computed: {
    friendlyName(): string {
      return camelToPascal(this.name);
    },
  },
  methods: {
    onAdd(): void {
      this.$emit("add-property", this.name, this.value);
    },
  },
});
</script>
