<script setup lang="ts">
import { SongDetailsModel } from "@/models/SongDetailsModel";
import { SongFilter } from "@/models/SongFilter";
import { SongHistory } from "@/models/SongHistory";
import { TrackModel } from "@/models/TrackModel";
import { getMenuContext } from "@/helpers/GetMenuContext";
import axios from "axios";
import { TypedJSON } from "typedjson";
import { ref, watch } from "vue";

const context = getMenuContext();

const title = ref("");
const artist = ref("");
const searching = ref(false);
const adding = ref(false);
const validated = ref(false);
const filter = new SongFilter();
const songs = ref<SongHistory[] | null>(null);
const tracks = ref<TrackModel[] | null>(null);
const service = ref<string | null>(null);

const emit = defineEmits<{
  "edit-song": [song: SongDetailsModel];
}>();

const onSubmit = async (event: Event): Promise<void> => {
  event.preventDefault();
  validated.value = true;

  const titleValue = title.value;
  const artistValue = artist.value;

  if (!titleValue || !artistValue) {
    return;
  }
  try {
    searching.value = true;
    const results = await context.axiosXsrf.get(
      `/api/song?title=${titleValue}&artist=${artistValue}`,
    );
    songs.value = TypedJSON.parseAsArray(results.data, SongHistory);
  } catch (e) {
    console.log("Search failed", e);
    songs.value = [];
  }

  searching.value = false;
};

const onReset = (event: Event): void => {
  event.preventDefault();
  reset();
};

const reset = (): void => {
  title.value = "";
  artist.value = "";
  searching.value = false;
  coreReset();
};

const coreReset = (): void => {
  validated.value = false;
  songs.value = null;
  tracks.value = null;
};

const editSong = (songId: string): void => {
  const history = songs.value?.find((s) => s.id === songId) as SongHistory;
  if (!history) {
    throw new Error(`Augment search in a bad state: unable to find ${songId}`);
  }
  const model = new SongDetailsModel({
    created: false,
    songHistory: history,
    filter: filter,
    userName: context.userName,
  });

  reset();
  emit("edit-song", model);
};

const searchSpotify = async (): Promise<void> => {
  await searchService("S");
};

const searchItunes = async (): Promise<void> => {
  await searchService("I");
};

const searchService = async (s: string): Promise<void> => {
  try {
    service.value = s;
    searching.value = true;
    const results = await axios.get(
      `/api/musicservice/?service=${s}&title=${title.value}&artist=${artist.value}`,
    );
    tracks.value = TypedJSON.parseAsArray(results.data, TrackModel);
  } catch {
    tracks.value = [];
    service.value = null;
  }
  searching.value = false;
};

const addTrack = async (track: TrackModel): Promise<void> => {
  adding.value = true;
  try {
    const uri = `/api/servicetrack/${service.value}${track.trackId}`;
    const response = await axios.get(uri);
    const songModel = TypedJSON.parse(response.data, SongDetailsModel);
    if (songModel) {
      emit("edit-song", songModel);
    }
  } catch (e) {
    console.log(e);
  }
  adding.value = false;
};

watch([artist, title], () => {
  coreReset();
});
</script>

<template>
  <div>
    <p>
      Search for a song by entering title, artist and optional album and clicking the
      <b>Search</b> button
    </p>
    <BForm :validated="validated" class="mb-3" novalidate @submit="onSubmit" @reset="onReset">
      <BFormGroup id="title-group" label="Title:" label-for="title">
        <BFormInput
          id="input-1"
          v-model="title"
          type="text"
          placeholder="A very danceable song"
          required
        />
        <div class="invalid-feedback">You must include a Title</div>
      </BFormGroup>

      <BFormGroup id="artist-group" label="Artist:" label-for="artist">
        <BFormInput
          id="artist"
          v-model="artist"
          type="text"
          placeholder="An amazing artist"
          required
        />
        <div class="invalid-feedback">You must include an Artist</div>
      </BFormGroup>

      <div class="mt-3 d-flex justify-content-between">
        <BButton type="submit" variant="primary" class="me-2">Search</BButton>
        <BButton type="reset" variant="danger">Reset</BButton>
      </div>
    </BForm>
    <BAlert v-show="searching && !songs" show variant="info">
      <BSpinner class="me-3" />
      <span>Searching for </span> {{ title }} by {{ artist }} in the music4dance catalog.
    </BAlert>
    <div v-if="songs && !tracks">
      <BAlert v-show="songs.length" show variant="success"
        >We found some songs in the music4dance catalog that may be a match. If one of the songs
        below is a match, please click the "Edit" button next the song, otherwise search on iTunes
        or spotify.</BAlert
      >
      <BAlert v-show="!songs.length" show variant="warning"
        >No songs matching, click one of the buttons below to search <b>Spotify</b> or
        <b>Apple Music</b>.</BAlert
      >
    </div>
    <div v-if="songs">
      <BAlert v-show="searching && songs" show variant="info">
        <BSpinner class="me-3" />
        <span>Searching for </span> {{ title }} by {{ artist }} in the publisher catalog.
      </BAlert>
      <BButton variant="secondary" class="me-2" @click="searchSpotify">Search Spotify</BButton>
      <BButton variant="secondary" @click="searchItunes">Search Apple Music</BButton>
    </div>
    <SongTable
      v-if="songs && songs.length && !tracks"
      :histories="songs as SongHistory[]"
      :filter="filter"
      :hide-sort="true"
      action="Edit"
      :hidden-columns="['dances', 'echo', 'edit', 'order', 'play', 'tags', 'track']"
      @song-selected="editSong"
    />
    <div v-if="tracks" class="my-2">
      <BAlert v-show="tracks.length" show variant="success"
        >We found some songs that may be a match. If one of the songs below is a match, please click
        the "Add" button next. Otherwise, try searching the other service (Apple Music or Spotify).
        If none of that works, try changing the title or artist - less is sometimes better, so
        perhaps just use artist last name or a fragment of the title.</BAlert
      >
      <BAlert v-show="!tracks.length" show variant="warning"
        >No matching songs. Please try searching the other service (Apple Music or Spotify). If none
        of that works, try changing the title or artist - less is sometimes better, so perhaps just
        use artist last name or a fragment of the title.</BAlert
      >
    </div>

    <BListGroup v-if="tracks && tracks.length" flush>
      <BListGroupItem v-for="track in tracks" :key="track.trackId">
        <TrackItem :track="track" @add-track="addTrack" />
      </BListGroupItem>
    </BListGroup>
  </div>
</template>

<style lang="scss" scoped>
#service-id {
  width: 25rem;
}
</style>
