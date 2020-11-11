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
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { DanceStats } from "@/model/DanceStats";

declare const environment: DanceEnvironment;

@Component
export default class SpotifyPlayer extends Vue {
  @Prop() private readonly playlist?: string;
  @Prop() private readonly danceId?: string;

  private get dance(): DanceStats | undefined {
    const id = this.danceId;
    return id ? environment.fromId(id) : undefined;
  }

  private get spotifyLink(): string | undefined {
    const id = this.playlist ?? this.dance?.spotifyPlaylist;
    return id ? `https://open.spotify.com/embed/playlist/${id}` : undefined;
  }
}
</script>
