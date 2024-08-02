<script setup lang="ts">
import DanceButton from "@/components/DanceButton.vue";
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

const danceHandler = new DanceHandler(DanceRating.fromTag(props.tag), props.tag.neutral);
const icon = props.tag.positive ? "i-bi-hand-thumbs-up" : "i-bi-hand-thumbs-down";
const color = props.tag.positive ? "green" : "red";
</script>

<template>
  <div>
    <IBiArrowCounterclockwise v-if="!added" :style="{ color: 'red', fontSize: '.75em' }" />
    <component :is="icon" :style="{ color: color }"></component>
    <DanceButton
      :dance-handler="danceHandler"
      class="ml-2"
      @dance-clicked="emit('dance-clicked', $event)"
    ></DanceButton>
  </div>
</template>
