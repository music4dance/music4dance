<script setup lang="ts">
import { computed } from "vue";
import type { SharedIdReviewSong, SharedIdServiceTrack } from "../SharedIdReviewModel";
import { lookupIds } from "../ClusterChoice";
import { formatLength } from "../format";

// What Spotify and iTunes say each of a song's ids is, once the cluster's ids have been looked up
const props = defineProps<{
  song: SharedIdReviewSong;
  tracks: Map<string, SharedIdServiceTrack>;
}>();

const found = computed(() =>
  lookupIds([props.song]).map((id) => props.tracks.get(id) ?? { id, found: false }),
);

// "Title — Artist · Album · 3:05 · 2020-01-02 · R:USRC1", leaving out what the service didn't say
function describe(track: SharedIdServiceTrack): string {
  if (!track.found) {
    return "not found";
  }
  const details = [
    track.album,
    formatLength(track.length),
    track.releaseDate,
    track.isrc ? `R:${track.isrc}` : "",
  ].filter((d) => !!d);
  return [`${track.title ?? ""} — ${track.artist ?? ""}`, ...details].join(" · ");
}
</script>

<template>
  <div v-for="track in found" :key="track.id" class="small service-track">
    <strong>{{ track.id }}</strong
    ><span :class="{ 'text-danger': !track.found }">{{ ` ${describe(track)}` }}</span>
  </div>
  <span v-if="found.length === 0" class="small text-muted">No Spotify or iTunes ids</span>
</template>
