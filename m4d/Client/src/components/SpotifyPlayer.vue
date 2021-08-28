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
import { Component, Vue, Prop } from "vue-property-decorator";

@Component
export default class SpotifyPlayer extends Vue {
  @Prop() private readonly playlist?: string;

  private get spotifyLink(): string | undefined {
    const id = this.playlist;
    return id ? `https://open.spotify.com/embed/playlist/${id}` : undefined;
  }
}
</script>
