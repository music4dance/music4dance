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
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";
import { Song } from "@/model/Song";
import { PurchaseInfo } from "@/model/Purchase";

@Component
export default class PlayModal extends Vue {
  @Prop() private readonly song!: Song;

  private get title(): string {
    return `${this.song.title} by ${this.song.artist}`;
  }

  private get purchaseInfo(): PurchaseInfo[] {
    return this.song.getPurchaseInfos();
  }

  private get playerId(): string {
    return `sample-player-${this.song.songId}`;
  }

  private onShown(): void {
    (this.$refs.player as HTMLAudioElement).play();
  }
}
</script>
