<template>
  <tag-selector
    :options="danceOptions"
    :showInitialList="true"
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
import TagSelector from "@/components/TagSelector.vue";
import { ListOption } from "@/model/ListOption";
import { NamedObject } from "@/model/NamedObject";
import { Component, Model, Prop, Vue } from "vue-property-decorator";

@Component({
  components: {
    TagSelector,
  },
})
export default class DanceSelector extends Vue {
  @Model("input") private readonly selected!: string[];
  @Prop() private readonly danceList!: NamedObject[];

  private get selectedInternal(): string[] {
    return this.selected;
  }

  private set selectedInternal(selected: string[]) {
    this.$emit("input", selected);
  }

  private get danceOptions(): ListOption[] {
    return this.danceList
      .map((d) => ({ text: d.name, value: d.id }))
      .sort((a, b) => a.text.localeCompare(b.text));
  }
}
</script>
