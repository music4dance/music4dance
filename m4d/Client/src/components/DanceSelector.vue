<template>
  <tag-selector
    :options="danceOptions"
    chooseLabel="Choose Dances"
    searchLabel="Search Dances"
    emptyLabel="No more dances to choose"
    variant="primary"
    class="mt-2"
    v-model="selectedInternal"
  >
  </tag-selector>
</template>

<script lang="ts">
import { Component, Prop, Model, Watch, Vue } from "vue-property-decorator";
import TagSelector from "@/components/TagSelector.vue";
import { ListOption } from "@/model/ListOption";
import { DanceObject } from "@/model/DanceStats";

@Component({
  components: {
    TagSelector,
  },
})
export default class DanceSelector extends Vue {
  @Model("change") private readonly selected!: string[];
  @Prop() private readonly danceList!: DanceObject[];

  private selectedInternal: string[];

  constructor() {
    super();

    this.selectedInternal = this.selected;
  }

  private get danceOptions(): ListOption[] {
    return this.danceList
      .map((d) => ({ text: d.name, value: d.id }))
      .sort((a, b) => a.text.localeCompare(b.text));
  }

  @Watch("selectedInternal")
  private onSelectedChanged(newVal: string[], oldVal: string[]): void {
    this.$emit("change", newVal);
  }
}
</script>
