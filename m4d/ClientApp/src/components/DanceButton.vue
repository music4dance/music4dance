<script setup lang="ts">
import { useTagButton } from "@/composables/useTagButton";
import { DanceHandler } from "@/models/DanceHandler";
import { computed } from "vue";

const props = defineProps<{
  danceHandler: DanceHandler;
}>();

const emit = defineEmits<{
  "dance-clicked": [tag: DanceHandler];
}>();

const { icon, tag, variant } = useTagButton(props.danceHandler);

const danceHandler = computed(() => props.danceHandler as DanceHandler);
const danceRating = computed(() => danceHandler.value.danceRating);
const weight = computed(() => (danceRating.value ? danceRating.value.weight : 0));
const hasTags = danceRating.value?.tags?.length > 0;
</script>

<template>
  <BButton
    :title="tag.value"
    :variant="variant"
    size="sm"
    style="margin-inline-end: 0.25em; margin-bottom: 0.25em"
    @click="emit('dance-clicked', danceHandler)"
  >
    <component :is="icon"></component>
    {{ tag.value }}
    <BBadge variant="light">{{ weight }}</BBadge>
    <IBiTagFill v-if="hasTags" style="margin-left: 0.25em"></IBiTagFill>
  </BButton>
</template>
