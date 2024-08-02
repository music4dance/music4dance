<script setup lang="ts">
import { SongFilter } from "@/models/SongFilter";

const props = defineProps<{
  filter: SongFilter;
  user?: string;
}>();

const changeLink = `/song/advancedsearchform?filter=${props.filter.encodedQuery}`;
const description = props.filter.description;
const playListRef = props.filter.getPlayListRef(props.user);
</script>

<template>
  <div>
    <h1>Search Results</h1>
    <h3>
      {{ description }}
      <BButton :href="changeLink" variant="primary" class="mx-1">Change</BButton>
      <BButton v-if="playListRef" :href="playListRef" class="mx-1">Create Spotify PlayList</BButton>
    </h3>
  </div>
</template>
