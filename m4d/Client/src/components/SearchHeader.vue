<template>
  <div>
    <h1>Search Results</h1>
    <h3>
      {{ description }}
      <b-button :href="changeLink" variant="primary" class="mx-1"
        >Change</b-button
      >
      <b-button v-if="playListRef" :href="playListRef" class="mx-1"
        >Create Spotify PlayList</b-button
      >
    </h3>
  </div>
</template>

<script lang="ts">
import { SongFilter } from "@/model/SongFilter";
import Vue, { PropType } from "vue";

export default Vue.extend({
  props: {
    filter: Object as PropType<SongFilter>,
    user: String,
  },
  computed: {
    changeLink(): string {
      return `/song/advancedsearchform?filter=${this.filter.encodedQuery}`;
    },
    description(): string {
      return this.filter.description;
    },
    playListRef(): string | undefined {
      return this.filter.getPlayListRef(this.user);
    },
  },
});
</script>
