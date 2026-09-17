<script setup lang="ts">
import { Song } from "@/models/Song";
import { cleanArtistName } from "@/models/ArtistNames";
import { computed, ref, watch } from "vue";

const props = defineProps<{
  song: Song;
}>();

const emit = defineEmits<{
  /** undefined or [] = hand the list back to the automatic splitter */
  "update-artists": [artists: string[] | undefined];
}>();

const sameList = (a: string[], b: string[]) =>
  a.length === b.length && a.every((value, index) => value === b[index]);

const tags = ref<string[]>([...props.song.effectiveArtists]);
watch(
  () => props.song.effectiveArtists,
  (artists) => {
    if (!sameList(artists, tags.value)) {
      tags.value = [...artists];
    }
  },
);

const sourceLabel = computed(() => {
  switch (props.song.artistsSource) {
    case "User":
      return "edited";
    case "Service":
      return "from a music service";
    case "Heuristic":
      return "automatic";
    default:
      return "automatic - single artist";
  }
});

const onTagsUpdated = (value: string[]) => {
  const cleaned = value.map(cleanArtistName).filter((a) => a.length > 0);
  if (!sameList(cleaned, props.song.effectiveArtists)) {
    emit("update-artists", cleaned);
  }
};

const resetToAutomatic = () => emit("update-artists", undefined);
const dontSplit = () => emit("update-artists", [cleanArtistName(props.song.artist)]);
</script>

<template>
  <div data-edit-target="song-artists">
    <label for="artists-editor" class="form-label mb-0">
      Individual artists <small class="text-body-secondary">({{ sourceLabel }})</small>
    </label>
    <BFormTags
      :model-value="tags"
      input-id="artists-editor"
      placeholder="Add an artist and press Enter"
      remove-on-delete
      size="sm"
      @update:model-value="onTagsUpdated($event as string[])"
    />
    <BButton
      v-if="song.artistsSource === 'User'"
      size="sm"
      variant="link"
      class="ps-0"
      @click="resetToAutomatic"
      >Reset to automatic</BButton
    >
    <BButton
      v-if="song.hasIndividualArtists"
      size="sm"
      variant="link"
      class="ps-0"
      @click="dontSplit"
      >Don't split</BButton
    >
  </div>
</template>
