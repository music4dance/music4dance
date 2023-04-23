<template>
  <div>
    <h2>{{ model?.name }}</h2>
    <p>id = ({{ id }}), count = {{ model?.tracks?.length ?? 0 }}</p>
    <p v-if="model?.description" v-html="model?.description"></p>
    <track-list :tracks="model?.tracks"></track-list>
    <b-form v-if="isAdmin" @submit="submit" @reset="reset">
      <h2>Add the playlist to music4dance</h2>
      <b-form-group
        id="danceTagsGroup"
        label="Dance Tags"
        label-for="danceTags"
        description="DID|dancetag|dancetag||DID2||DID3|dancetag... where tag = tag:type and type = Style, Tempo, or Other"
      >
        <b-form-input
          id="danceTags"
          v-model="danceTags"
          type="text"
          placeholder="SWZ|Slow:Style||VWZ|Fast:Style"
        ></b-form-input>
      </b-form-group>
      <b-form-group
        id="songTagsGroup"
        label="Song Tags"
        label-for="songTags"
        description="tag|tag... where tag = tag:type and type = Tempo, Other, or Music"
      >
        <b-form-input
          id="songTags"
          v-model="songTags"
          type="text"
          placeholder="Holiday:Other|Christmas:Other"
        ></b-form-input>
      </b-form-group>

      <b-button
        :disabled="disableAdd"
        type="submit"
        variant="primary"
        class="mr-2"
        >Add</b-button
      >
      <b-button :disabled="disableAdd" type="reset" variant="outline-danger"
        >Reset</b-button
      >
    </b-form>
  </div>
</template>

<script lang="ts">
import { PlaylistModel } from "@/model/PlaylistModel";
import { PropType } from "vue";
import TrackList from "./TrackList.vue";
import axios from "axios";
import AdminTools from "@/mix-ins/AdminTools";

//DID|dancetag|dancetag||DID2||DID3|dancetag where tag = tag:type and type = Style, Tempo, or Other
// tag|tag... where tag = tag:type and type = Tempo, Other, or Music

export default AdminTools.extend({
  components: {
    TrackList,
  },
  props: {
    id: String,
    model: Object as PropType<PlaylistModel | null>,
  },
  data() {
    return new (class {
      danceTags = "";
      songTags = "";
    })();
  },
  computed: {
    disableAdd(): boolean {
      return !this.danceTags && !this.songTags;
    },
  },
  methods: {
    async submit(event: Event): Promise<void> {
      event.preventDefault();
      const tags =
        this.danceTags + (this.songTags ? `|||${this.songTags}` : "");
      const url = `/api/serviceplaylist?id=s${this.id}&tags=${tags}`;
      try {
        await axios.post(url);
        this.$bvModal.msgBoxOk(`Playlist ${this.id} added to music4dance`);
      } catch (e) {
        this.$bvModal.msgBoxOk(
          `Failed to add playlist ${this.id} to music4dance`
        );
        // eslint-disable-next-line no-console
        console.log(e);
      }
      this.danceTags = "";
      this.songTags = "";
    },
    reset(event: Event): void {
      event.preventDefault();
      this.danceTags = "";
      this.songTags = "";
    },
  },
});
</script>
