<template>
  <a
    v-if="playlist.music4danceId"
    :href="m4dLink"
    target="_blank"
    role="button"
    class="ml-1"
  >
    <b-iconstack font-scale="1.75">
      <b-icon stacked icon="circle"></b-icon>
      <b-icon stacked icon="music-note" scale=".75"></b-icon>
    </b-iconstack>
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
      return `/playlist/details/${this.playlist.id}`;
    },
  },
});
</script>
