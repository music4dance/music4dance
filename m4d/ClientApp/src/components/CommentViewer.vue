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
</script>

<template>
  <div style="display: flex">
    <IBiPatchPlus v-if="props.added" :style="{ color: 'green' }" />
    <IBiPatchMinus v-else :style="{ color: 'red' }" />
    <span>
      {{ comment }}
      <span v-if="danceId"> on <DanceButton :dance-handler="danceHandler!" /></span>
    </span>
  </div>
</template>
