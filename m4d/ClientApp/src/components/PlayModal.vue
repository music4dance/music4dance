<script setup lang="ts">
import { Song } from "@/models/Song";
import { ServiceType } from "@/models/Purchase";
import { computed, ref } from "vue";
import AddToPlaylistButton from "./AddToPlaylistButton.vue";

const props = defineProps<{
  song: Song;
}>();

const modelValue = defineModel<boolean>({ default: false });

const player = ref<HTMLAudioElement | null>(null);
const title = computed(() => `${props.song.title} by ${props.song.artist}`);
const purchaseInfo = computed(() => props.song.getPurchaseInfos());
const playerId = computed(() => `sample-player-${props.song.songId}`);
const hasSpotifyTrack = computed(() => {
  return purchaseInfo.value.some((p) => p.service === ServiceType.Spotify);
});

const onShown = () => {
  //const player = document.getElementById(playerId) as HTMLAudioElement;
  if (player.value) {
    player.value.play();
  }
};

const onHidden = () => {
  //const player = document.getElementById(playerId) as HTMLAudioElement;
  if (player.value) {
    player.value.pause();
  }
};
</script>

<template>
  <BModal
    v-model="modelValue"
    :title="title"
    ok-only
    ok-title="Close"
    @shown="onShown"
    @hidden="onHidden"
  >
    <BListGroup>
      <BListGroupItem v-for="pi in purchaseInfo" :key="pi.songId" :href="pi.link" target="_blank">
        <img width="32" height="32" :src="pi.logo" />
        Available on {{ pi.name }}
      </BListGroupItem>
    </BListGroup>

    <!-- Add to Playlist button -->
    <div v-if="hasSpotifyTrack" class="my-3">
      <AddToPlaylistButton
        :purchase-infos="purchaseInfo"
        :song-id="song.songId"
        :show-text="true"
        variant="primary"
      />
    </div>

    <!-- Audio player -->
    <div v-if="song.hasSample">
      <audio :id="playerId" ref="player" :src="song.sample" controls style="margin-top: 1em">
        <source type="audio/mpeg" />
        Your browser does not support audio
      </audio>
    </div>
  </BModal>
</template>
