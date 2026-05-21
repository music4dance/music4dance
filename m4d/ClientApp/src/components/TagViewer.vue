<script setup lang="ts">
import { DanceHandler } from "@/models/DanceHandler";
import { DanceRating } from "@/models/DanceRating";
import { Tag } from "@/models/Tag";
import { TagHandler } from "@/models/TagHandler";
import { computed } from "vue";

const props = defineProps<{
  tag: Tag;
  added?: boolean;
  danceId?: string;
  activeTags?: Set<string>;
}>();

const emit = defineEmits<{
  "tag-clicked": [tag: TagHandler];
}>();

const tagHandler = new TagHandler({ tag: props.tag });
const danceHandler = props.danceId
  ? new DanceHandler({
      danceRating: new DanceRating({ danceId: props.danceId }),
      tag: Tag.fromDanceId(props.danceId),
    })
  : undefined;
const color = props.added ? "green" : "red";
// Only strike through song-level tags (no danceId). Dance-qualified tags like
// Tag+:JIV=Modern:Style are never tracked in activeTags (which is song-level only),
// so they would always appear removed without this guard.
const isRemoved = computed(
  () =>
    !props.danceId &&
    !!props.activeTags &&
    !!props.added &&
    !props.activeTags.has(props.tag.toString()),
);
</script>

<template>
  <div style="display: flex">
    <IBiPatchPlus v-if="added" :style="{ color: color }" />
    <IBiPatchMinus v-else :style="{ color: color }" />
    <span :class="{ 'text-decoration-line-through text-muted': isRemoved }">
      <TagButton
        :tag-handler="tagHandler"
        class="ms-2"
        @tag-clicked="emit('tag-clicked', $event)"
      />
      <span v-if="danceId"> on <DanceButton :dance-handler="danceHandler!" /></span>
    </span>
  </div>
</template>
