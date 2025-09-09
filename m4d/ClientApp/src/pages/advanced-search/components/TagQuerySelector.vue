<script setup lang="ts">
import { ref, watch, computed } from "vue";
import TagCategorySelector from "@/components/TagCategorySelector.vue";
import type { Tag } from "@/models/Tag";
import { TagContext } from "@/models/Tag";

const props = defineProps<{
  modelValue: string;
  tagList: Tag[];
  context?: TagContext; // New prop to specify dance or song context
}>();

// Filter tags based on context (dance vs song)
const filteredTagList = computed(() => {
  if (!props.context) return props.tagList;
  return props.tagList.filter((tag) => {
    return props.context === TagContext.Dance ? tag.isValidForDance : tag.isValidForSong;
  });
});

const emit = defineEmits(["update:modelValue"]);

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
        :tag-list="filteredTagList"
        :context="context"
        choose-label="Choose Tags to Include"
        search-label="Search Tags"
        empty-label="No more tags to choose"
      />
    </BFormGroup>
    <BFormGroup label="Exclude Tags:">
      <TagCategorySelector
        v-model="excludeTags"
        :tag-list="filteredTagList"
        :context="context"
        choose-label="Choose Tags to Exclude"
        search-label="Search Tags"
        empty-label="No more tags to choose"
      />
    </BFormGroup>
  </div>
</template>
