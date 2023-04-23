<template>
  <b-table ref="table" striped hover :items="enhancedTracks" :fields="fields">
    <template v-slot:cell(m4d)="data">
      <song-button :track="data.item" @update-track="updateTrack"></song-button>
    </template>
  </b-table>
</template>

<script lang="ts">
import { EnhancedTrackModel, TrackModel } from "@/model/TrackModel";
import { BvTableFieldArray } from "bootstrap-vue";
import SongButton from "./SongButton.vue";
import Vue, { PropType } from "vue";
import { TypedJSON } from "typedjson";

export default Vue.extend({
  components: { SongButton },
  props: {
    tracks: [] as PropType<TrackModel[]>,
  },
  data() {
    return new (class {
      songIds: string[] = [];
    })();
  },
  computed: {
    fields(): BvTableFieldArray {
      return [
        { key: "m4d", label: "m4d" },
        { key: "name", sortable: true },
        { key: "artist", sortable: true },
        { key: "album", sortable: true },
        { key: "duration", sortable: true },
        {
          label: "BPM",
          key: "audioData.beatsPerMinute",
          sortable: true,
          formatter: (value: number) => value.toFixed(1),
        },
        { label: "Meter", key: "audioData.beatsPerMeasure", sortable: true },
        { label: "Dancability", key: "audioData.danceability", sortable: true },
        { label: "Energy", key: "audioData.energy", sortable: true },
        { label: "Valence", key: "audioData.valence", sortable: true },
      ];
    },
    enhancedTracks(): EnhancedTrackModel[] {
      return this.tracks
        .map((t, idx) => {
          const nt = TypedJSON.parse(
            TypedJSON.stringify(t, TrackModel),
            EnhancedTrackModel
          );
          if (!nt) {
            throw new Error(`Unable to parse track ${t.trackId}`);
          }
          nt.songId = this.songIds[idx] ?? "unknown";
          return nt;
        })
        .slice();
    },
  },
  methods: {
    updateTrack(trackId: string, songId: string): void {
      const idx = this.tracks.findIndex((t) => t.trackId === trackId);
      if (idx !== -1) {
        const songIds = this.songIds.slice();
        songIds[idx] = songId;
        this.songIds = songIds;
      }
    },
  },
});
</script>
