<script setup lang="ts">
import PlaylistButton from "./PlaylistButton.vue";
import PlaylistLink from "./PlaylistLink.vue";
import { SimplePlaylist } from "@/models/SimplePlaylist";

defineProps<{
  playlists: SimplePlaylist[];
}>();

const fields = [
  { key: "music4danceId", label: "m4d" },
  { key: "id", sortable: true },
  { key: "name", sortable: true },
  { key: "trackCount", label: "#", sortable: true },
  { key: "owner", sortable: true },
  { key: "description", sortable: false },
];
</script>

<template>
  <BTable ref="table" striped hover :items="playlists" :fields="fields">
    <template #cell(music4danceId)="data">
      <PlaylistButton :playlist="data.item"></PlaylistButton>
    </template>
    <template #cell(id)="data">
      <PlaylistLink :playlist="data.item"></PlaylistLink>
    </template>
  </BTable>
</template>
