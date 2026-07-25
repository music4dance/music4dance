<script setup lang="ts">
const props = defineProps<{ playlist?: string; isAlbum?: boolean }>();
const spotifyLink = props.playlist
  ? `https://open.spotify.com/embed/${props.isAlbum ? "album" : "playlist"}/${props.playlist}`
  : undefined;
</script>

<template>
  <section v-if="spotifyLink" id="spotify-player">
    <iframe
      :src="spotifyLink"
      frameborder="0"
      width="300"
      height="80"
      allowtransparency="true"
      allow="encrypted-media"
    />
  </section>
</template>
