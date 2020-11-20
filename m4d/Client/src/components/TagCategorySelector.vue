<template>
  <tag-selector
    :options="tagOptions"
    :chooseLabel="chooseLabel"
    :searchLabel="searchLabel"
    :emptyLabel="emptyLabel"
    variant="primary"
    class="mt-2"
    v-model="selectedInternal"
  >
    <template v-slot:default="{ tag, removeTag, disabled }">
      <b-form-tag
        @remove="removeTag(tag)"
        :title="titleFromKey(tag)"
        :disabled="disabled"
        :variant="variantFromKey(tag)"
      >
        <b-icon :icon="iconFromKey(tag)"></b-icon>
        {{ titleFromKey(tag) }}
      </b-form-tag>
    </template>
    <template v-slot:option="{ option, addTag, onOptionClick }">
      <b-dropdown-item-button
        :key="option.value"
        :variant="variantFromKey(option.value)"
        @click="onOptionClick(option, addTag)"
      >
        <b-icon :icon="iconFromKey(option.value)"></b-icon>
        {{ descriptionFromKey(option.value) }}
      </b-dropdown-item-button>
    </template>
  </tag-selector>
</template>

<script lang="ts">
import { Component, Prop, Model, Watch, Vue } from "vue-property-decorator";
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

  @Model("change") private readonly selected!: string[];
  @Prop() private readonly tagList!: Tag[];
  @Prop() private readonly chooseLabel!: string;
  @Prop() private readonly searchLabel!: string;
  @Prop() private readonly emptyLabel!: string;

  private selectedInternal: string[];
  private tagOptions: ListOption[];
  private tagMap: Map<string, Tag>;

  constructor() {
    super();

    this.tagOptions = this.buildTagOptions();
    this.tagMap = this.buildTagMap();
    this.selectedInternal = this.selected;
  }

  private buildTagOptions(): ListOption[] {
    return this.tagList
      .filter((t) =>
        TagCategorySelector.categories.has(t.category.toLowerCase())
      )
      .map((t) => ({ text: t.value, value: this.keyFromTag(t) }));
  }

  private buildTagMap(): Map<string, Tag> {
    return new Map<string, Tag>(
      this.tagList.map((t) => [this.keyFromTag(t), t])
    );
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

  private descriptionFromKey(key: string): string {
    const ret =
      `${this.titleFromKey(key)} ` +
      `(${Tag.TagInfo.get(this.variantFromKey(key))!.description})`;
    return ret;
  }

  private keyFromTag(tag: Tag): string {
    return `${tag.value}:${tag.category}`;
  }

  private tagFromKey(key: string): Tag {
    return this.tagMap.get(key)!;
  }

  @Watch("selectedInternal")
  private onSelectedChanged(newVal: string[]): void {
    this.$emit("change", newVal);
  }
}
</script>
