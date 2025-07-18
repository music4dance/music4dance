<script setup lang="ts">
import { EnhancedTrackModel } from "@/models/TrackModel";
import { SongDetailsModel } from "@/models/SongDetailsModel";
import { TypedJSON } from "typedjson";
import { computed, onMounted } from "vue";
import { getAxiosXsrf } from "@/helpers/GetMenuContext";

const props = defineProps<{
  track: EnhancedTrackModel;
}>();

const emit = defineEmits<{
  "update-track": [trackId: string, songId: string];
}>();

const songId = computed(() => props.track.songId ?? null);
const gotoSong = computed(() => `/song/details/${props.track.songId}`);
const addSong = computed(() => `/song/augment?id=${props.track.trackId}`);

onMounted(async () => {
  const uri = `/api/servicetrack/${props.track.serviceType}${props.track.trackId}?localOnly=true`;
  let songId = "notfound";
  try {
    const response = await getAxiosXsrf().get(uri);
    const songModel = TypedJSON.parse(response.data, SongDetailsModel);
    if (songModel) {
      console.log(`Found: ${props.track.trackId}`);
      songId = songModel.songHistory.id;
    }
  } catch (error) {
    console.log(`Not Found: ${props.track.trackId} (${error})`);
  }
  emit("update-track", props.track.trackId, songId);
});
</script>

<template>
  <div>
    <BSpinner v-if="songId === 'unknown'" small />
    <a v-else-if="songId === 'notfound'" :href="addSong" target="_blank" role="button" class="ms-1">
      <IBiPlusCircle />
    </a>
    <a v-else :href="gotoSong" target="_blanks" role="button" class="ms-1">
      <NoteCircle />
    </a>
  </div>
</template>
