<script setup lang="ts">
import { Song } from "@/models/Song";
import { artistCreditLayout, artistPageUrl, cleanArtistName } from "@/models/ArtistNames";
import { computed } from "vue";

const props = defineProps<{
  song: Song;
  /** Link each individual artist rather than the whole credit (ArtistIndex feature flag) */
  individual?: boolean;
}>();

const credit = computed(() => cleanArtistName(props.song.artist));
const layout = computed(() =>
  props.individual && props.song.hasIndividualArtists
    ? artistCreditLayout(credit.value, props.song.effectiveArtists)
    : undefined,
);
</script>

<template>
  <span v-if="layout"
    ><template v-for="(segment, index) in layout.segments" :key="index"
      ><a v-if="segment.artist" :href="artistPageUrl(segment.artist)">{{ segment.text }}</a
      ><template v-else>{{ segment.text }}</template></template
    ><template v-if="layout.extra.length">
      (with
      <template v-for="(artist, index) in layout.extra" :key="artist"
        ><template v-if="index > 0">, </template
        ><a :href="artistPageUrl(artist)">{{ artist }}</a></template
      >)</template
    ></span
  >
  <a v-else-if="credit" :href="artistPageUrl(credit)">{{ credit }}</a>
</template>
