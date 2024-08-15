<script setup lang="ts">
import { PlaylistModel } from "@/models/PlaylistModel";
import TrackList from "./TrackList.vue";
import axios from "axios";
import { computed, ref } from "vue";
import { getMenuContext } from "@/helpers/GetMenuContext";

//DID|dancetag|dancetag||DID2||DID3|dancetag where tag = tag:type and type = Style, Tempo, or Other
// tag|tag... where tag = tag:type and type = Tempo, Other, or Music
const context = getMenuContext();

const props = defineProps<{
  id: string;
  model: PlaylistModel | null;
}>();

const danceTags = ref("");
const songTags = ref("");

const disableAdd = computed(() => !danceTags.value && !songTags.value);

const submit = async (event: Event): Promise<void> => {
  event.preventDefault();
  const tags = danceTags.value + (songTags.value ? `|||${songTags.value}` : "");
  const url = `/api/serviceplaylist?id=s${props.id}&tags=${tags}`;
  try {
    await axios.post(url);
    alert(`Playlist ${props.id} added to music4dance`);
  } catch (e) {
    alert(`Failed to add playlist ${props.id} to music4dance`);
    // eslint-disable-next-line no-console
    console.log(e);
  }
  danceTags.value = "";
  songTags.value = "";
};

const reset = (event: Event): void => {
  event.preventDefault();
  danceTags.value = "";
  songTags.value = "";
};
</script>

<template>
  <div>
    <h2>{{ model?.name }}</h2>
    <p>id = ({{ id }}), count = {{ model?.tracks?.length ?? 0 }}</p>
    <p v-if="model?.description" v-html="model?.description"></p>
    <TrackList :tracks="model?.tracks ?? []"></TrackList>
    <BForm v-if="context.isAdmin" @submit="submit" @reset="reset">
      <h2>Add the playlist to music4dance</h2>
      <BFormGroup
        id="danceTagsGroup"
        label="Dance Tags"
        label-for="danceTags"
        description="DID|dancetag|dancetag||DID2||DID3|dancetag... where tag = tag:type and type = Style, Tempo, or Other"
      >
        <BFormInput
          id="danceTags"
          v-model="danceTags"
          type="text"
          placeholder="SWZ|Slow:Style||VWZ|Fast:Style"
        ></BFormInput>
      </BFormGroup>
      <BFormGroup
        id="songTagsGroup"
        label="Song Tags"
        label-for="songTags"
        description="tag|tag... where tag = tag:type and type = Tempo, Other, or Music"
      >
        <BFormInput
          id="songTags"
          v-model="songTags"
          type="text"
          placeholder="Holiday:Other|Christmas:Other"
        ></BFormInput>
      </BFormGroup>

      <BButton :disabled="disableAdd" type="submit" variant="primary" class="me-2">Add</BButton>
      <BButton :disabled="disableAdd" type="reset" variant="outline-danger">Reset</BButton>
    </BForm>
  </div>
</template>
