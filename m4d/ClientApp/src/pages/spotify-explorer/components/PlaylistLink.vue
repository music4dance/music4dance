<script setup lang="ts">
import { SimplePlaylist } from "@/models/SimplePlaylist";
import { computed } from "vue";

const props = defineProps<{
  playlist: SimplePlaylist;
}>();

const m4dLink = computed(
  () => `/home/spotifyexplorer?link=https://open.spotify.com/playlist/${props.playlist.id}`,
);
</script>

<template>
  <a :href="m4dLink" target="_blank" class="ms-1">
    {{ playlist.id }}
  </a>
</template>
