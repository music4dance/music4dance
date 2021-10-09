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
import { Component, Model, Prop, Vue, Watch } from "vue-property-decorator";

@Component
export default class FormList extends Vue {
  @Model("change") private readonly selected!: string[];
  @Prop() private type!: string;
  @Prop() private options!: ListOption[];

  private allSelected = false;
  private indeterminate = false;
  private selectedInternal: string[] = [];

  private mounted(): void {
    this.selectedInternal = this.selected;
    this.allSelected = this.selectedInternal.length === this.options.length;
    this.indeterminate =
      !this.allSelected && this.selectedInternal.length !== 0;
  }

  private toggleAll(checked: boolean): void {
    this.selectedInternal = checked ? this.allValues : [];
  }

  @Watch("selectedInternal")
  private onSelectedChanged(newVal: string[]): void {
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
  }

  private get title(): string {
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
  }

  private get allDescription(): string {
    return "All " + this.pluralType;
  }

  private get noDescription(): string {
    return "No " + this.pluralType;
  }

  private get pluralType(): string {
    return this.type + "s";
  }

  private get allValues(): string[] {
    return valuesFromOptions(this.options);
  }
}
</script>
