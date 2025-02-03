<script setup lang="ts">
import { DanceHandler } from "@/models/DanceHandler";
import { DanceRating } from "@/models/DanceRating";
import { Tag } from "@/models/Tag";

const props = defineProps<{
  tag: Tag;
  added?: boolean;
}>();

const emit = defineEmits<{
  "dance-clicked": [tag: DanceHandler];
}>();

const danceRating = DanceRating.fromTag(props.tag);
const danceHandler = danceRating
  ? new DanceHandler({ danceRating, tag: props.tag.neutral })
  : undefined;
</script>

<template>
  <div>
    <span class="me-2">
      <IBiArrowCounterclockwise v-if="!added" :style="{ color: 'red', fontSize: '.75em' }" />
      <IBiHandThumbsUp v-if="props.tag.positive" :style="{ color: 'green' }" />
      <IBiHandThumbsDown v-else :style="{ color: 'red' }" />
    </span>
    <DanceButton
      v-if="danceHandler"
      :dance-handler="danceHandler"
      class="ms-2"
      @dance-clicked="emit('dance-clicked', $event)"
    />
  </div>
</template>
