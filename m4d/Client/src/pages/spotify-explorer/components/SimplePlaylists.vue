<template>
  <b-table ref="table" striped hover :items="playlists" :fields="fields">
    <template v-slot:cell(music4danceId)="data">
      <playlist-button :playlist="data.item"></playlist-button>
    </template>
    <template v-slot:cell(id)="data">
      <playlist-link :playlist="data.item"></playlist-link>
    </template>
  </b-table>
</template>

<script lang="ts">
import PlaylistButton from "./PlaylistButton.vue";
import PlaylistLink from "./PlaylistLink.vue";
import { BvTableFieldArray } from "bootstrap-vue";
import Vue, { PropType } from "vue";
import { SimplePlaylist } from "@/model/SimplePlaylist";

export default Vue.extend({
  components: { PlaylistButton, PlaylistLink },
  props: {
    playlists: [] as PropType<SimplePlaylist[]>,
  },
  computed: {
    fields(): BvTableFieldArray {
      return [
        { key: "music4danceId", label: "m4d" },
        { key: "id", sortable: true },
        { key: "name", sortable: true },
        { key: "trackCount", label: "#", sortable: true },
        { key: "owner", sortable: true },
        { key: "description", sortable: false },
      ];
    },
  },
});
</script>
