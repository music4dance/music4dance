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
import Vue, { PropType } from "vue";

export default Vue.extend({
  components: {
    TagSelector,
  },
  model: {
    prop: "selected",
    event: "input",
  },
  props: {
    selected: [] as PropType<string[]>,
    danceList: {
      type: [] as PropType<NamedObject[]>,
      required: true,
    },
  },
  computed: {
    selectedInternal: {
      get: function (): string[] {
        return this.selected;
      },
      set: function (selected: NamedObject[]): void {
        this.$emit("input", selected);
      },
    },
    danceOptions(): ListOption[] {
      return this.danceList
        .map((d) => ({ text: d.name, value: d.id }))
        .sort((a, b) => a.text.localeCompare(b.text));
    },
  },
});
</script>
