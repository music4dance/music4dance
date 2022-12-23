<template>
  <b-card
    header="Changes"
    header-text-variant="primary"
    no-body
    border-variant="primary"
  >
    <b-list-group flush>
      <b-list-group-item
        v-for="(change, index) in history.userChanges"
        :key="index"
      >
        <song-change-viewer :change="change"></song-change-viewer>
      </b-list-group-item>
    </b-list-group>
  </b-card>
</template>

<script lang="ts">
import { SongHistory } from "@/model/SongHistory";
import "reflect-metadata";
import Vue, { PropType } from "vue";
import SongChangeViewer from "./SongChangeViewer.vue";

export default Vue.extend({
  components: { SongChangeViewer },
  props: { history: { type: Object as PropType<SongHistory>, required: true } },
});
</script>
