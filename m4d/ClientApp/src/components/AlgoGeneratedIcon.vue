<script setup lang="ts">
import { Song } from "@/models/Song";
import { PropertyType } from "@/models/SongProperty";

defineProps<{
  song: Song;
  stacked?: boolean;
}>();

const docUrl = "https://music4dance.blog/music4dance-help/song-list/#tempo-note";

const shouldShow = (song: Song): boolean => {
  return !!song.tempo && !song.isUserModified(PropertyType.tempoField);
};
</script>

<template>
  <a
    v-if="shouldShow(song)"
    :href="docUrl"
    target="_blank"
    title="This tempo was algorithmically generated. Click to learn more."
    :class="['algo-generated-icon', { stacked }]"
  >
    <IBiCpuFill />
  </a>
</template>

<style scoped>
.algo-generated-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.8em;
  height: 1.8em;
  margin-left: 0.5em;
  border-radius: 50%;
  background-color: rgba(108, 117, 125, 0.1);
  border: 1px solid rgba(108, 117, 125, 0.2);
  text-decoration: none;
  transition: all 0.2s ease-in-out;
  cursor: pointer;
}

.algo-generated-icon.stacked {
  margin: 0.25em auto 0 auto;
}

.algo-generated-icon:hover {
  background-color: rgba(108, 117, 125, 0.2);
  border-color: rgba(108, 117, 125, 0.4);
  transform: scale(1.1);
}

.algo-generated-icon svg {
  width: 1em;
  height: 1em;
  color: #6c757d;
}

.algo-generated-icon:hover svg {
  color: #495057;
}
</style>
