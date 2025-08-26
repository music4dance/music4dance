<script setup lang="ts">
import { ref, watch } from "vue";
import TagCategorySelector from "@/components/TagCategorySelector.vue";
import type { Tag } from "@/models/Tag";

const props = defineProps<{
  modelValue: string;
  tagList: Tag[];
}>();

const emit = defineEmits(["update:modelValue"]);

=
const extractTags = (tags: string, include: boolean): string[] => {
  if (!tags) return [];
  const qualifier = include ? "+" : "-";
  const parts = tags.split("|").map((p) => p.trim());
  let filtered = parts.filter((p) => p.startsWith(qualifier)).map((p) => p.slice(1));
  if (include) {
    filtered = filtered.concat(parts.filter((p) => !p.startsWith("+") && !p.startsWith("-")));
  }
  return filtered;
};

const buildSingleTagList = (tags: string[], decorator: string): string => {
  return tags.map((t) => `${decorator}${t}`).join("|");
};

const includeTags = ref<string[]>(extractTags(props.modelValue, true));
const excludeTags = ref<string[]>(extractTags(props.modelValue, false));

watch(
  () => props.modelValue,
  (val) => {
    includeTags.value = extractTags(val, true);
    excludeTags.value = extractTags(val, false);
  },
);

watch([includeTags, excludeTags], () => {
  const lists: string[] = [];
  if (includeTags.value.length > 0) lists.push(buildSingleTagList(includeTags.value, "+"));
  if (excludeTags.value.length > 0) lists.push(buildSingleTagList(excludeTags.value, "-"));
  emit("update:modelValue", lists.join("|"));
});
</script>

<template>
  <div>
    <BFormGroup label="Include Tags:">
      <TagCategorySelector
        v-model="includeTags"
        :tag-list="tagList"
        choose-label="Choose Tags to Include"
        search-label="Search Tags"
        empty-label="No more tags to choose"
      />
    </BFormGroup>
    <BFormGroup label="Exclude Tags:">
      <TagCategorySelector
        v-model="excludeTags"
        :tag-list="tagList"
        choose-label="Choose Tags to Exclude"
        search-label="Search Tags"
        empty-label="No more tags to choose"
      />
    </BFormGroup>
  </div>
</template>
