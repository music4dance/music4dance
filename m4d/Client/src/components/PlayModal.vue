<template>
  <b-modal
    :id="song.songId"
    :title="title"
    ok-only
    ok-title="Close"
    @shown="onShown"
  >
    <b-list-group>
      <b-list-group-item
        v-for="pi in purchaseInfo"
        :key="pi.songId"
        :href="pi.link"
        target="_blank"
      >
        <img width="32" height="32" :src="pi.logo" />
        Available on {{ pi.name }}
      </b-list-group-item>
      <div v-if="song.hasSample">
        <audio
          :id="playerId"
          ref="player"
          :src="song.sample"
          controls
          style="margin-top: 1em"
        >
          <source type="audio/mpeg" />
          Your browser does not support audio
        </audio>
      </div>
    </b-list-group>
  </b-modal>
</template>

<script lang="ts">
import { PurchaseInfo } from "@/model/Purchase";
import { Song } from "@/model/Song";
import "reflect-metadata";
import Vue, { PropType } from "vue";

export default Vue.extend({
  props: {
    song: { type: Object as PropType<Song>, required: true },
  },
  computed: {
    title(): string {
      return `${this.song.title} by ${this.song.artist}`;
    },

    purchaseInfo(): PurchaseInfo[] {
      return this.song.getPurchaseInfos();
    },

    playerId(): string {
      return `sample-player-${this.song.songId}`;
    },
  },
  methods: {
    onShown(): void {
      const player = this.$refs.player as HTMLAudioElement;
      if (player) {
        player.play();
      }
    },
  },
});
</script>
