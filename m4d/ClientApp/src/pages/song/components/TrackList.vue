<script setup lang="ts">
import { Song } from "@/models/Song";
import { TrackModel } from "@/models/TrackModel";
import axios from "axios";
import { TypedJSON } from "typedjson";
import { computed, ref } from "vue";
import TrackItem from "./TrackItem.vue";

const props = defineProps<{
  song: Song;
  editing?: boolean;
}>();

const tracks = ref<TrackModel[]>([]);
const error = ref<string>("");
const title = ref<string>("");
const artist = ref<string>("");

const newTracks = computed(() => {
  const albums = props.song.albums;
  if (!albums) {
    return tracks.value;
  }

  return tracks.value.filter(
    (track) =>
      !albums.find((album) => {
        const purchaseList = album.purchase.decode();
        return purchaseList.find(
          (purchase) => purchase.service === track.serviceType && purchase.songId === track.trackId,
        );
      }),
  );
});

const lookup = async (service?: string): Promise<void> => {
  try {
    error.value = "";
    let parameters = service ? `service=${service}&` : "";
    const titleValue = title.value;
    if (titleValue) {
      parameters += `title=${encodeURIComponent(titleValue)}&`;
    }
    const artistValue = artist.value;
    if (artistValue) {
      parameters += `artist=${encodeURIComponent(artistValue)}&`;
    }
    const results = await axios.get(`/api/musicservice/${props.song.songId}?${parameters}`);
    tracks.value = TypedJSON.parseAsArray(results.data, TrackModel);
  } catch (e: unknown) {
    error.value = e as string;
    tracks.value = [];
  }
};
</script>

<template>
  <BCard header="Tracks" header-text-variant="primary" no-body border-variant="primary">
    <BForm class="m-2">
      <BFormInput
        id="track-title"
        v-model="title"
        class="mb-2"
        placeholder="Title Override"
      ></BFormInput>

      <BFormInput
        id="track-artist"
        v-model="artist"
        class="mb-2"
        placeholder="Artist Override"
      ></BFormInput>

      <div class="d-inline-flex gap-2">
        <BButton variant="primary" @click="lookup()">Lookup</BButton>
        <BButton variant="outline-primary" @click="lookup('S')">Spotify</BButton>
        <BButton variant="outline-primary" @click="lookup('I')">iTunes</BButton>
      </div>
      <BAlert v-if="error" show variant="danger" class="mt-2">{{ error }}</BAlert>
      <BAlert v-if="tracks.length > 0" show variant="success" class="mt-2">
        {{ tracks.length }} track found. {{ newTracks.length }} not already added.
      </BAlert>
    </BForm>
    <BListGroup flush>
      <BListGroupItem v-for="track in newTracks" :key="track.trackId">
        <TrackItem :track="track" :enable-properties="true" v-bind="$attrs" />
      </BListGroupItem>
    </BListGroup>
  </BCard>
</template>
