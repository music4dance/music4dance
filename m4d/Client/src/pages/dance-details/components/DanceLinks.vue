<template>
  <div id="references">
    <h2>References:</h2>
    <div v-for="(link, index) in links" :key="index">
      <edittable-link
        v-model="internalLinks[index]"
        :editing="editing"
      ></edittable-link>
    </div>
    <b-button
      v-if="editing"
      block
      variant="outline-primary"
      class="mt-2"
      @click="onAdd"
      >Add Reference</b-button
    >
  </div>
</template>

<script lang="ts">
import { jsonCompare } from "@/helpers/ObjectHelpers";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceLink } from "@/model/DanceLink";
import { Editor } from "@/model/Editor";
import "reflect-metadata";
import { Component, Mixins, Model, Prop, Watch } from "vue-property-decorator";
import EdittableLink from "./EdittableLink.vue";

@Component({
  components: {
    EdittableLink,
  },
})
export default class DanceLinks
  extends Mixins(EnvironmentManager)
  implements Editor
{
  @Model("update") readonly links!: DanceLink[];
  @Prop() private readonly danceId!: string;
  @Prop() private readonly editing!: boolean;
  private initialLinks?: DanceLink[];

  private mounted(): void {
    this.initialLinks = this.cloneLinks(this.links);
  }

  public get isModified(): boolean {
    return !jsonCompare(this.links, this.initialLinks);
  }

  public commit(): void {
    this.initialLinks = this.cloneLinks(this.links);
  }

  private get internalLinks(): DanceLink[] {
    return this.links;
  }

  private set internalLinks(value: DanceLink[]) {
    this.$emit("update", value);
  }

  @Watch("editing")
  onEditChanged(val: boolean): void {
    if (val === false && this.initialLinks) {
      this.$emit("update", this.cloneLinks(this.initialLinks));
    }
  }

  private onAdd(): void {
    this.$emit("update", [
      ...this.cloneLinks(this.links),
      new DanceLink({ danceId: this.danceId }),
    ]);
  }

  private cloneLinks(value: DanceLink[]): DanceLink[] {
    return value.map((l) => new DanceLink(l));
  }
}
</script>
