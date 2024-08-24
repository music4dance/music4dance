<script setup lang="ts">
import { DanceHandler } from "@/models/DanceHandler";
import { DanceRating } from "@/models/DanceRating";
import { Tag } from "@/models/Tag";
import { TagHandler } from "@/models/TagHandler";

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
const color = props.added ? "green" : "red";
</script>

<template>
  <div style="display: flex">
    <IBiPatchPlus v-if="added" :style="{ color: color }" />
    <IBiPatchMinus v-else :style="{ color: color }" />
    <span>
      <TagButton
        :tag-handler="tagHandler"
        class="ms-2"
        @tag-clicked="emit('tag-clicked', $event)"
      />
      <span v-if="danceId"> on <DanceButton :dance-handler="danceHandler!" /></span>
    </span>
  </div>
</template>
