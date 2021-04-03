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
import { Component, Prop, Model, Vue } from "vue-property-decorator";
import TagSelector from "@/components/TagSelector.vue";
import { ListOption } from "@/model/ListOption";
import { Tag } from "@/model/Tag";

@Component({
  components: {
    TagSelector,
  },
})
export default class TagCategorySelector extends Vue {
  private static categories = new Set(Tag.tagKeys.filter((k) => k !== "dance"));

  @Model("input") private readonly selected!: string[];
  @Prop() private readonly tagList!: Tag[];
  @Prop() private readonly chooseLabel!: string;
  @Prop() private readonly searchLabel!: string;
  @Prop() private readonly emptyLabel!: string;
  @Prop() private readonly addCategories?: string[];

  private get selectedInternal(): string[] {
    return this.selected;
  }

  private set selectedInternal(selected: string[]) {
    this.$emit("input", selected);
  }

  private get tagMap(): Map<string, Tag> {
    return new Map<string, Tag>(this.tagList.map((t) => [t.key, t]));
  }

  private getTagOptions(): ListOption[] {
    return this.tagList
      .filter((t) =>
        TagCategorySelector.categories.has(t.category.toLowerCase())
      )
      .map((t) => ({ text: t.value, value: t.key }));
  }

  private titleFromKey(key: string): string {
    return this.tagFromKey(key).value;
  }

  private variantFromKey(key: string): string {
    return this.tagFromKey(key).category.toLowerCase();
  }

  private iconFromKey(key: string): string {
    return Tag.TagInfo.get(this.variantFromKey(key))!.iconName;
  }

  private descriptionFromOption(option: ListOption): string {
    const ret =
      `${
        option.text.startsWith("+") ? option.text.substring(1) : option.text
      } ` +
      `(${Tag.TagInfo.get(this.variantFromKey(option.value))!.description})`;
    return ret;
  }

  private addFromOption(option: ListOption): boolean {
    return option.text.startsWith("+");
  }

  private tagFromKey(key: string): Tag {
    return this.tagMap.get(key) ?? Tag.fromString(key);
  }
}
</script>
