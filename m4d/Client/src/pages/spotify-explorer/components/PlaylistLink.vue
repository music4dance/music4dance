<template>
  <a :href="m4dLink" target="_blank" class="ml-1">
    {{ playlist.id }}
  </a>
</template>

<script lang="ts">
import { SimplePlaylist } from "@/model/SimplePlaylist";
import Vue, { PropType } from "vue";

export default Vue.extend({
  components: {},
  props: {
    playlist: Object as PropType<SimplePlaylist>,
  },
  computed: {
    m4dLink(): string {
      return `/home/spotifyexplorer?link=https://open.spotify.com/playlist/${this.playlist.id}`;
    },
  },
});
</script>
