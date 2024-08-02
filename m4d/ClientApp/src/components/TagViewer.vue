<script setup lang="ts">
import DanceButton from "@/components/DanceButton.vue";
import TagButton from "@/components/TagButton.vue";
import { DanceHandler } from "@/models/DanceHandler";
import { DanceRating } from "@/models/DanceRating";
import { Tag } from "@/models/Tag";
import { TagHandler } from "@/models/TagHandler";

// INT-TODO: Where is this used? If it is used, we'll want to update TagButton
const props = defineProps<{
  tag: Tag;
  added?: boolean;
  danceId?: string;
}>();

const emit = defineEmits<{
  "tag-clicked": [tag: TagHandler];
}>();

const tagHandler = new TagHandler(props.tag);
const danceHandler = props.danceId
  ? new DanceHandler(new DanceRating({ danceId: props.danceId }), Tag.fromDanceId(props.danceId))
  : undefined;
const icon = props.added ? "i-bi-patch-plus" : "i-bi-patch-minus";
const color = props.added ? "green" : "red";
</script>

<template>
  <div style="display: flex">
    <component :is="icon" :style="{ color: color }" />
    <span>
      <TagButton
        :tag-handler="tagHandler"
        class="ml-2"
        @tag-clicked="emit('tag-clicked', $event)"
      ></TagButton>
      <span v-if="danceId"> on <DanceButton :dance-handler="danceHandler!"></DanceButton></span>
    </span>
  </div>
</template>
