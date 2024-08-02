<script setup lang="ts">
import { useTagButton } from "@/composables/useTagButton";
import type { TagHandler } from "@/models/TagHandler";

const props = defineProps<{
  tagHandler: TagHandler;
}>();

const emit = defineEmits<{
  "tag-clicked": [tag: TagHandler];
}>();

const { icon, tag, variant, selectedIcon } = useTagButton(props.tagHandler);
</script>

<template>
  <BButton
    :title="tag.value"
    :variant="variant"
    size="sm"
    style="margin-inline-end: 0.25em; margin-bottom: 0.25em"
    @click="emit('tag-clicked', tagHandler)"
  >
    <component :is="icon"></component>
    {{ tag.value }}
    <component :is="selectedIcon" v-if="selectedIcon" />
  </BButton>
</template>
