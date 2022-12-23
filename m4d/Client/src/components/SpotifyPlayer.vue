<template>
  <section v-if="spotifyLink" id="spotify-player">
    <iframe
      :src="spotifyLink"
      frameborder="0"
      width="300"
      height="80"
      allowtransparency="true"
      allow="encrypted-media"
    ></iframe>
  </section>
</template>

<script lang="ts">
import "reflect-metadata";
import Vue from "vue";

export default Vue.extend({
  props: { playlist: String },
  computed: {
    spotifyLink(): string | undefined {
      const id = this.playlist;
      return id ? `https://open.spotify.com/embed/playlist/${id}` : undefined;
    },
  },
});
</script>
