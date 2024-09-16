<script setup lang="ts">
import { DanceRatingVote } from "@/models/DanceRatingDelta";
import { SongEditor } from "@/models/SongEditor";
import { SongFilter } from "@/models/SongFilter";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { computed } from "vue";

const context = getMenuContext();

const props = defineProps<{
  editor: SongEditor;
  filter: SongFilter;
}>();

const emit = defineEmits<{
  "show-play": [songId: string];
  "show-like": [songId: string];
  "dance-vote": [vote: DanceRatingVote];
}>();

const song = computed(() => props.editor.song);
const danceId = computed(() => props.filter.danceQuery.danceList[0]);
const danceRating = computed(() => song.value.findDanceRatingById(danceId.value)!);
</script>

<template>
  <span>
    <DanceVote
      v-if="filter.singleDance && danceRating"
      :vote="editor.song.danceVote(danceId)"
      :dance-rating="danceRating"
      :authenticated="!!context.userName"
      style="margin-right: 0.25em"
      @dance-vote="emit('dance-vote', $event)"
    />
    <SongLikeButton
      :song="song"
      :user="context.userName!"
      :scale="1.5"
      @click-like="() => emit('show-like', song.songId)"
    />
    <a href="#" role="button" class="ms-1" @click="emit('show-play', song.songId)">
      <IBiPlayCircleFill class="ms-1" :style="{ fontSize: '1.5em' }" />
    </a>
  </span>
</template>
