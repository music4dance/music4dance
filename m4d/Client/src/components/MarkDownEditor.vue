<template>
  <div>
    <b-form-textarea
      v-if="editting"
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
import "reflect-metadata";
import { Component, Mixins, Prop, Model, Watch } from "vue-property-decorator";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceText } from "@/model/DanceText";
import { Editor } from "@/model/Editor";

// TODO: If we want to use this in places other than dance description, we should generalize
//  the "expand" api...
@Component
export default class MarkDownEditor
  extends Mixins(EnvironmentManager)
  implements Editor {
  @Model("input") readonly value!: string;
  @Prop() private readonly editting!: boolean;
  private initialDescription: string;

  public constructor() {
    super();
    this.initialDescription = this.value;
  }

  public get isModified(): boolean {
    return this.initialDescription !== this.value;
  }

  public commit(): void {
    this.initialDescription = this.value;
  }

  private get descriptionInternal(): string {
    return this.value;
  }

  private set descriptionInternal(value: string) {
    this.$emit("input", value);
  }

  private get descriptionExpanded(): string {
    return new DanceText(this.value).expanded();
  }

  @Watch("editting")
  onEditChanged(val: boolean): void {
    if (val === false) {
      this.$emit("input", this.initialDescription);
    }
  }
}
</script>
