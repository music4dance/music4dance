<script setup lang="ts">
import { SimplePlaylist } from "@/models/SimplePlaylist";
import { computed } from "vue";

const props = defineProps<{
  playlist: SimplePlaylist;
}>();

const m4dLink = computed(() => `/playlist/details/${props.playlist.id}`);
</script>

<template>
  <a v-if="playlist.music4danceId" :href="m4dLink" target="_blank" role="button" class="ms-1">
    <NoteCircle />
  </a>
</template>
