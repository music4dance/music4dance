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
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";
import { SongHistory } from "@/model/SongHistory";
import SongChangeViewer from "./SongChangeViewer.vue";

@Component({ components: { SongChangeViewer } })
export default class SongHistoryViewer extends Vue {
  @Prop() private readonly history!: SongHistory;
}
</script>
