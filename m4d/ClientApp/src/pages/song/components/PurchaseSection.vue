<script setup lang="ts">
import { computed } from "vue";
import { PurchaseInfo, ServiceType } from "@/models/Purchase";
import { SongFilter } from "@/models/SongFilter";
import AddToPlaylistButton from "@/components/AddToPlaylistButton.vue";

const props = defineProps<{
  purchaseInfos: PurchaseInfo[];
  filter: SongFilter;
  songId: string;
}>();

const hasSpotifyTrack = computed(() =>
  props.purchaseInfos.some((pi) => pi.service === ServiceType.Spotify),
);
</script>

<template>
  <div>
    <span v-for="pi in purchaseInfos" :key="pi.service" class="mx-auto my-1" style="width: 110px">
      <PurchaseLogo
        :info="pi"
        :is-charm="true"
        styles="margin-right: .25rem; margin-bottom: .25rem"
      />
    </span>
    <div v-if="hasSpotifyTrack" class="my-2">
      <AddToPlaylistButton
        :purchase-infos="purchaseInfos"
        :song-id="songId"
        variant="primary"
        size="md"
      />
    </div>
    <BButton
      v-if="filter && !filter.isEmpty"
      :href="filter.url"
      small
      variant="primary"
      class="my-1"
      ><IBiSearch aria-hidden="true" /> Back to Search</BButton
    >
    <BButton
      variant="outline-primary"
      class="ms-1 my-1"
      small
      href="https://music4dance.blog/music4dance-help/song-details/"
      target="_blank"
      ><IBiQuestionCircle aria-hidden="true" /> Help</BButton
    >
  </div>
</template>
