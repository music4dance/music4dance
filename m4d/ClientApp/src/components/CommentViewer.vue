<script setup lang="ts">
import DanceButton from "@/components/DanceButton.vue";
import { DanceHandler } from "@/models/DanceHandler";
import { DanceRating } from "@/models/DanceRating";
import { Tag } from "@/models/Tag";

const props = defineProps<{
  comment: string;
  added?: boolean;
  danceId?: string;
}>();
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
      {{ comment }}
      <span v-if="danceId"> on <DanceButton :dance-handler="danceHandler!"></DanceButton></span>
    </span>
  </div>
</template>
