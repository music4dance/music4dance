<script setup lang="ts">
import TagButton from "@/components/TagButton.vue";
import { SongFilter } from "@/models/SongFilter";
import { Tag } from "@/models/Tag";
import { TaggableObject } from "@/models/TaggableObject";
import { TagHandler } from "@/models/TagHandler";
import { computed } from "vue";

const props = defineProps<{
  container: TaggableObject;
  filter?: SongFilter;
  user?: string;
}>();

const emit = defineEmits<{
  "tag-clicked": [tag: TagHandler];
}>();
const tags = computed(() => {
  const ret = props.container.tags.filter((t) => t.category && t.category !== "Dance");
  return ret;
});

const tagHandler = (tag: Tag): TagHandler => {
  return new TagHandler(tag, props.user, props.filter, props.container);
};
</script>

<template>
  <span>
    <TagButton
      v-for="tag in tags"
      :key="tag.key"
      :tag-handler="tagHandler(tag)"
      @tag-clicked="emit('tag-clicked', $event)"
    />
  </span>
</template>
