<script setup lang="ts">
const props = defineProps<{ playlist?: string }>();
const spotifyLink = props.playlist
  ? `https://open.spotify.com/embed/playlist/${props.playlist}`
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
    ></iframe>
  </section>
</template>
