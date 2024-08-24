<script setup lang="ts">
import { EnhancedTrackModel, TrackModel } from "@/models/TrackModel";
import { TypedJSON } from "typedjson";
import { computed, ref } from "vue";

const props = defineProps<{
  tracks: TrackModel[];
}>();

const songIds = ref<string[]>([]);

const fields = [
  { key: "m4d", label: "m4d" },
  { key: "name", sortable: true },
  { key: "artist", sortable: true },
  { key: "album", sortable: true },
  { key: "duration", sortable: true },
  {
    label: "BPM",
    key: "audioData.beatsPerMinute",
    sortable: true,
    formatter: (value: unknown) => (value as number).toFixed(1),
  },
  { label: "Meter", key: "audioData.beatsPerMeasure", sortable: true },
  { label: "Dancability", key: "audioData.danceability", sortable: true },
  { label: "Energy", key: "audioData.energy", sortable: true },
  { label: "Valence", key: "audioData.valence", sortable: true },
];

const enhancedTracks = computed(() => {
  return props.tracks
    .map((t, idx) => {
      const nt = TypedJSON.parse(TypedJSON.stringify(t, TrackModel), EnhancedTrackModel);
      if (!nt) {
        throw new Error(`Unable to parse track ${t.trackId}`);
      }
      nt.songId = songIds.value[idx] ?? "unknown";
      return nt;
    })
    .slice();
});

const updateTrack = (trackId: string, songId: string): void => {
  const idx = props.tracks.findIndex((t) => t.trackId === trackId);
  if (idx !== -1) {
    const ids = songIds.value.slice();
    ids[idx] = songId;
    songIds.value = ids;
  }
};
</script>

<template>
  <BTable ref="table" striped hover :items="enhancedTracks" :fields="fields">
    <template #cell(m4d)="data">
      <SongButton :track="data.item" @update-track="updateTrack" />
    </template>
  </BTable>
</template>
