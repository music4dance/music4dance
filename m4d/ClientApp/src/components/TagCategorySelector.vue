<script setup lang="ts">
import { type ListOption } from "@/models/ListOption";
import { Tag, TagContext } from "@/models/Tag";
import type { ColorVariant } from "bootstrap-vue-next";
import { computed } from "vue";

const model = defineModel<string[]>();
const props = defineProps<{
  tagList: Tag[];
  searchLabel: string;
  chooseLabel: string;
  emptyLabel: string;
  addCategories?: string[];
  context?: TagContext | TagContext[]; // Can specify one or multiple contexts
}>();

// Get valid categories based on context
const categories = computed(() => {
  if (!props.context) {
    return new Set(Tag.tagKeys.filter((k) => k !== "dance"));
  }

  const contextArray = Array.isArray(props.context) ? props.context : [props.context];
  let validCategories: string[] = [];

  for (const context of contextArray) {
    if (context === TagContext.Dance) {
      validCategories.push(...Tag.getDanceValidCategories());
    } else if (context === TagContext.Song) {
      validCategories.push(...Tag.getSongValidCategories());
    }
  }

  // Remove duplicates
  validCategories = [...new Set(validCategories)];

  return new Set(validCategories);
});

const tagMap = computed(() => {
  return new Map<string, Tag>(props.tagList.map((t) => [t.key, t]));
});

function getTagOptions(): ListOption[] {
  return props.tagList
    .filter((t) => categories.value.has(t.category.toLowerCase()))
    .map((t) => ({ text: t.value, value: t.key }));
}

function titleFromKey(key: string): string {
  return tagFromKey(key).value;
}

function variantFromKey(key: string): ColorVariant {
  return tagFromKey(key).category.toLowerCase() as ColorVariant;
}

function iconFromKey(key: string): string {
  return Tag.tagInfo.get(variantFromKey(key))!.iconName;
}

function descriptionFromOption(option: ListOption): string {
  const ret =
    `${option.text.startsWith("+") ? option.text.substring(1) : option.text} ` +
    `(${Tag.tagInfo.get(variantFromKey(option.value))!.description})`;
  return ret;
}

function addFromOption(option: ListOption): boolean {
  return option.text.startsWith("+");
}

function tagFromKey(key: string): Tag {
  return tagMap.value.get(key) ?? Tag.fromString(key);
}
</script>

<template>
  <TagSelector
    v-model="model"
    :options="getTagOptions()"
    :choose-label="chooseLabel"
    :search-label="searchLabel"
    :empty-label="emptyLabel"
    :add-categories="addCategories"
    variant="primary"
  >
    <template #default="{ tag, removeTag, disabled }">
      <BFormTag
        :title="titleFromKey(tag)"
        :disabled="disabled"
        :variant="variantFromKey(tag)"
        data-bs-theme="dark"
        @remove="removeTag(tag)"
      >
        {{ titleFromKey(tag) }}
      </BFormTag>
    </template>
    <template #option="{ option, addTag, onOptionClick }">
      <BDropdownItemButton
        :key="option.value"
        :variant="variantFromKey(option.value)"
        @click="onOptionClick(option, addTag)"
      >
        <IBiPlusCircle v-if="addFromOption(option)" variant="danger" class="me-1" />
        <TagIcon :name="iconFromKey(option.value)" />
        {{ descriptionFromOption(option) }}
      </BDropdownItemButton>
    </template>
  </TagSelector>
</template>
