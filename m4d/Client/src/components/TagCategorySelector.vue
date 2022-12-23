<template>
  <tag-selector
    :options="getTagOptions()"
    :chooseLabel="chooseLabel"
    :searchLabel="searchLabel"
    :emptyLabel="emptyLabel"
    :addCategories="addCategories"
    variant="primary"
    v-model="selectedInternal"
  >
    <template v-slot:default="{ tag, removeTag, disabled }">
      <b-form-tag
        @remove="removeTag(tag)"
        :title="titleFromKey(tag)"
        :disabled="disabled"
        :variant="variantFromKey(tag)"
      >
        {{ titleFromKey(tag) }}
      </b-form-tag>
    </template>
    <template v-slot:option="{ option, addTag, onOptionClick }">
      <b-dropdown-item-button
        :key="option.value"
        :variant="variantFromKey(option.value)"
        @click="onOptionClick(option, addTag)"
      >
        <b-icon-plus-circle
          variant="danger"
          v-if="addFromOption(option)"
          class="mr-1"
        ></b-icon-plus-circle>
        <b-icon :icon="iconFromKey(option.value)"></b-icon>
        {{ descriptionFromOption(option) }}
      </b-dropdown-item-button>
    </template>
  </tag-selector>
</template>

<script lang="ts">
import TagSelector from "@/components/TagSelector.vue";
import { ListOption } from "@/model/ListOption";
import { Tag } from "@/model/Tag";
import Vue, { PropType } from "vue";

const categories = new Set(Tag.tagKeys.filter((k) => k !== "dance"));

export default Vue.extend({
  components: { TagSelector },
  model: {
    prop: "selected",
    event: "input",
  },
  props: {
    selected: { type: Array as PropType<string[]>, required: true },
    tagList: { type: Array as PropType<Tag[]>, required: true },
    chooseLabel: String,
    searchLabel: String,
    emptyLabel: String,
    addCategories: Array as PropType<string[]>,
  },
  data() {
    return new (class {})();
  },
  computed: {
    selectedInternal: {
      get: function (): string[] {
        return this.selected;
      },
      set: function (selected: string[]): void {
        this.$emit("input", selected);
      },
    },
    tagMap(): Map<string, Tag> {
      return new Map<string, Tag>(this.tagList.map((t) => [t.key, t]));
    },
  },
  methods: {
    getTagOptions(): ListOption[] {
      return this.tagList
        .filter((t) => categories.has(t.category.toLowerCase()))
        .map((t) => ({ text: t.value, value: t.key }));
    },

    titleFromKey(key: string): string {
      return this.tagFromKey(key).value;
    },

    variantFromKey(key: string): string {
      return this.tagFromKey(key).category.toLowerCase();
    },

    iconFromKey(key: string): string {
      return Tag.TagInfo.get(this.variantFromKey(key))!.iconName;
    },

    descriptionFromOption(option: ListOption): string {
      const ret =
        `${
          option.text.startsWith("+") ? option.text.substring(1) : option.text
        } ` +
        `(${Tag.TagInfo.get(this.variantFromKey(option.value))!.description})`;
      return ret;
    },

    addFromOption(option: ListOption): boolean {
      return option.text.startsWith("+");
    },

    tagFromKey(key: string): Tag {
      return this.tagMap.get(key) ?? Tag.fromString(key);
    },
  },
});
</script>
