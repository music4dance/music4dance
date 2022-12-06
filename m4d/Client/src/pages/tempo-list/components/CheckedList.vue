<template>
  <b-dropdown
    id="dropdown-form"
    :text="title"
    ref="dropdown"
    variant="primary"
    style="margin-bottom: 8px"
  >
    <b-dropdown-form>
      <b-form-checkbox
        v-model="allSelected"
        :indeterminate="indeterminate"
        aria-describedby="selected"
        aria-controls="selected"
        @change="toggleAll"
      >
        {{ allSelected ? "Un-select All" : "Select All" }}
      </b-form-checkbox>
      <hr />
      <b-form-group>
        <b-form-checkbox-group
          v-model="selectedInternal"
          :options="options"
          name="temp"
          stacked
        ></b-form-checkbox-group>
      </b-form-group>
    </b-dropdown-form>
  </b-dropdown>
</template>

<script lang="ts">
import { ListOption, valuesFromOptions } from "@/model/ListOption";
import "reflect-metadata";
import Vue, { PropType } from "vue";

export default Vue.extend({
  model: {
    prop: "selected",
    event: "change",
  },
  props: {
    selected: { type: [] as PropType<string[]>, required: true },
    type: { type: String, required: true },
    options: { type: [] as PropType<ListOption[]>, required: true },
  },
  data() {
    return new (class {
      allSelected = false;
      indeterminate = false;
      selectedInternal: string[] = [];
    })();
  },
  computed: {
    title(): string {
      if (this.allSelected) {
        return this.allDescription;
      } else if (this.indeterminate) {
        if (this.selectedInternal.length === 1) {
          const el = this.options.find(
            (e) => e.value === this.selectedInternal[0]
          );
          return el!.text;
        } else {
          return this.selectedInternal.length + " " + this.pluralType;
        }
      } else {
        return this.noDescription;
      }
    },
    allDescription(): string {
      return "All " + this.pluralType;
    },
    noDescription(): string {
      return "No " + this.pluralType;
    },
    pluralType(): string {
      return this.type + "s";
    },
    allValues(): string[] {
      return valuesFromOptions(this.options);
    },
  },
  methods: {
    toggleAll(checked: boolean): void {
      this.selectedInternal = checked ? this.allValues : [];
    },
  },
  watch: {
    selectedInternal(newVal: string[]): void {
      if (newVal.length === 0) {
        this.indeterminate = false;
        this.allSelected = false;
      } else if (newVal.length === this.options.length) {
        this.indeterminate = false;
        this.allSelected = true;
      } else {
        this.indeterminate = true;
        this.allSelected = false;
      }
      this.$emit("change", newVal);
    },
  },
  mounted(): void {
    this.selectedInternal = this.selected;
    this.allSelected = this.selectedInternal.length === this.options.length;
    this.indeterminate =
      !this.allSelected && this.selectedInternal.length !== 0;
  },
});
</script>
