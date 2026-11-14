<script setup lang="ts">
import { computed } from "vue";
import type { ClusterChoice } from "../ClusterChoice";
import type { SharedIdReviewSong } from "../SharedIdReviewModel";

// A song's albums with their song-level ids. Given a choice (a song column), each album and id
// has a box that removes it; without one (the If merged column) it's read-only.
const props = defineProps<{
  song: SharedIdReviewSong;
  choice?: ClusterChoice;
  // Names the song in the boxes' labels: "song 2"
  label?: string;
}>();

const songId = computed(() => props.song.songId);
const albums = computed(() => props.song.albums);

function albumRemoved(album: number): boolean {
  return props.choice?.isAlbumRemoved(songId.value, album) ?? false;
}

function idRemoved(album: number, id: string): boolean {
  return props.choice?.isIdRemoved(songId.value, album, id) ?? false;
}

function onAlbum(album: number, value: unknown): void {
  props.choice?.setAlbumRemoved(songId.value, album, !!value);
}

function onId(album: number, id: string, value: unknown): void {
  props.choice?.setIdRemoved(songId.value, album, id, !!value);
}
</script>

<template>
  <div
    v-for="album in albums"
    :key="album.index"
    class="shared-id-album"
    :class="{ 'text-decoration-line-through': albumRemoved(album.index) }"
  >
    <BFormCheckbox
      v-if="choice"
      inline
      class="me-1"
      :model-value="albumRemoved(album.index)"
      :aria-label="`Remove ${album.name} from ${label}`"
      :title="`Remove ${album.name} from ${label}`"
      @update:model-value="onAlbum(album.index, $event)"
    >
      {{ album.name }}<template v-if="album.track"> #{{ album.track }}</template>
    </BFormCheckbox>
    <span v-else
      >{{ album.name }}<template v-if="album.track"> #{{ album.track }}</template></span
    >
    <span
      v-for="trackId in album.ids"
      :key="trackId.id"
      class="text-nowrap shared-id-track me-2"
      :class="{ 'text-decoration-line-through': idRemoved(album.index, trackId.id) }"
    >
      <BFormCheckbox
        v-if="choice"
        inline
        class="me-0"
        :model-value="idRemoved(album.index, trackId.id)"
        :disabled="albumRemoved(album.index)"
        :aria-label="`Remove ${trackId.id} from ${label}`"
        :title="`Remove ${trackId.id} from ${label}`"
        @update:model-value="onId(album.index, trackId.id, $event)"
      />
      <component
        :is="trackId.shared ? 'mark' : 'span'"
        :class="['small', { 'text-muted': !trackId.shared }]"
      >
        <a v-if="trackId.link" :href="trackId.link" target="_blank" rel="noopener noreferrer">{{
          trackId.id
        }}</a>
        <template v-else>{{ trackId.id }}</template>
      </component>
    </span>
  </div>
</template>
