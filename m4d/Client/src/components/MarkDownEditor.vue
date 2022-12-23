<template>
  <div>
    <b-form-textarea
      v-if="editing"
      v-model="descriptionInternal"
      rows="5"
      max-rows="10"
    ></b-form-textarea>
    <vue-showdown
      id="description"
      :markdown="descriptionExpanded"
    ></vue-showdown>
  </div>
</template>

<script lang="ts">
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceText } from "@/model/DanceText";
import "reflect-metadata";

// TODO: If we want to use this in places other than dance description, we should generalize
//  the "expand" api...
export default EnvironmentManager.extend({
  model: {
    prop: "value",
    event: "input",
  },
  props: {
    value: { type: String, required: true },
    editing: Boolean,
  },
  data() {
    return new (class {
      initialDescription = "";
    })();
  },
  computed: {
    descriptionInternal: {
      get: function (): string {
        return this.value;
      },
      set: function (value: string): void {
        this.$emit("input", value);
      },
    },
    isModified(): boolean {
      return this.initialDescription !== this.value;
    },
    descriptionExpanded(): string {
      return new DanceText(this.value).expanded();
    },
  },
  watch: {
    editing(val: boolean): void {
      if (val === false) {
        this.$emit("input", this.initialDescription);
      }
    },
  },
  methods: {
    commit(): void {
      this.initialDescription = this.value;
    },
  },
  mounted(): void {
    this.initialDescription = this.value;
  },
});
</script>
