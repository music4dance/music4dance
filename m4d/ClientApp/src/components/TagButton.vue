<script setup lang="ts">
import { useTagButton } from "@/composables/useTagButton";
import type { TagHandler } from "@/models/TagHandler";

const props = defineProps<{
  tagHandler: TagHandler;
}>();

const emit = defineEmits<{
  "tag-clicked": [tag: TagHandler];
}>();

const { icon, tag, variant, isSelected } = useTagButton(props.tagHandler);
</script>

<template>
  <BButton
    :title="tag.value"
    :variant="variant"
    size="sm"
    style="margin-inline-end: 0.25em; margin-bottom: 0.25em"
    @click.prevent="emit('tag-clicked', tagHandler)"
  >
    <TagIcon :name="icon" />
    {{ tag.value }}
    <IBiCheckCircle v-if="isSelected" />
  </BButton>
</template>
