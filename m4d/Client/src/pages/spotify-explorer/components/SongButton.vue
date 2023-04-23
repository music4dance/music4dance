<template>
  <div>
    <b-spinner v-if="songId === 'unknown'" small></b-spinner>
    <a
      v-else-if="songId === 'notfound'"
      :href="addSong"
      target="_blank"
      role="button"
      class="ml-1"
    >
      <b-icon-plus-circle-fill font-scale="1.75"> </b-icon-plus-circle-fill>
    </a>
    <a v-else :href="gotoSong" target="_blanks" role="button" class="ml-1">
      <b-iconstack font-scale="1.75">
        <b-icon stacked icon="circle"></b-icon>
        <b-icon stacked icon="music-note" scale=".75"></b-icon>
      </b-iconstack>
    </a>
  </div>
</template>

<script lang="ts">
import axios from "axios";
import { EnhancedTrackModel } from "@/model/TrackModel";
import Vue, { PropType } from "vue";
import { SongDetailsModel } from "@/model/SongDetailsModel";
import { TypedJSON } from "typedjson";

export default Vue.extend({
  components: {},
  props: {
    track: Object as PropType<EnhancedTrackModel>,
  },
  computed: {
    songId(): string | null {
      return this.track.songId ?? null;
    },
    gotoSong(): string {
      return `/song/details/${this.track.songId}`;
    },
    addSong(): string {
      return `/song/augment?id=${this.track.trackId}`;
    },
  },
  async mounted() {
    const uri = `/api/servicetrack/${this.track.serviceType}${this.track.trackId}?localOnly=true`;
    let songId = "notfound";
    try {
      const response = await axios.get(uri);
      const songModel = TypedJSON.parse(response.data, SongDetailsModel);
      if (songModel) {
        // eslint-disable-next-line no-console
        console.log(`Found: ${this.track.trackId}`);
        songId = songModel.songHistory.id;
      }
    } catch (error) {
      // eslint-disable-next-line no-console
      console.log(`Not Found: ${this.track.trackId}`);
    }
    this.$emit("update-track", this.track.trackId, songId);
  },
});
</script>
