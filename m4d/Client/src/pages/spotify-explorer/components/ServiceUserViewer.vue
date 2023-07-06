<template>
  <div>
    <h2>{{ model?.name }}</h2>
    <p>id = ({{ model?.id }}), count = {{ model?.playlists?.length ?? 0 }}</p>
    <simple-playlists :playlists="model?.playlists"></simple-playlists>

    <b-input-group v-if="isAdmin">
      <label class="sr-only" for="service-id">Friendly Name:</label>
      <b-form-input
        id="friendly-name"
        palceholder="UserName"
        v-model="friendlyName"
        aria-describedby="service-id-feedback"
        trim
        class="mr-2"
        @input="buildExport"
      ></b-form-input>
      <b-input-group-append class="mr-2">
        <b-button variant="primary" @click="buildExport"
          >Build Exports</b-button
        >
      </b-input-group-append>
    </b-input-group>
    <b-form-textarea
      id="export"
      v-model="exportText"
      rows="10"
      max-rows="100"
    ></b-form-textarea>
  </div>
</template>

<script lang="ts">
import mixins from "vue-typed-mixins";
import AdminTools from "@/mix-ins/AdminTools";
import { PropType } from "vue";
import SimplePlaylists from "./SimplePlaylists.vue";
import { ServiceUser } from "@/model/ServiceUser";

const playlistHeader = "+++++PLAYLISTS+++++";

export default mixins(AdminTools).extend({
  components: {
    SimplePlaylists,
  },
  props: {
    model: Object as PropType<ServiceUser | null>,
  },
  data() {
    const name = this.model?.name;
    return new (class {
      friendlyName = name;
      exportText = "";
    })();
  },
  computed: {
    friendlyNameState(): boolean | null {
      return this.friendlyName ? !!this.friendlyName : null;
    },
  },
  methods: {
    buildExport(): void {
      const playlists = this.model?.playlists;
      const id = this.model?.id;

      if (!playlists || !id) return;

      const rows = playlists
        .filter((p) => !p.music4danceId && p.owner === id)
        .map(
          (p) =>
            `${this.friendlyName}\t${p.name}\thttps://open.spotify.com/playlist/${p.id}\t${id}\t${p.trackCount}\t${p.description}`
        );

      this.exportText = playlistHeader + "\r\n" + rows.join("\r\n");
    },
  },
});
</script>
