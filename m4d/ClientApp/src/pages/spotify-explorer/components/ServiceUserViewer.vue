<script setup lang="ts">
import { getMenuContext } from "@/helpers/GetMenuContext";
import { ref } from "vue";
import SimplePlaylists from "./SimplePlaylists.vue";
import { ServiceUser } from "@/models/ServiceUser";

const playlistHeader = "+++++PLAYLISTS+++++";

const props = defineProps<{
  model: ServiceUser | null;
}>();

const context = getMenuContext();

const friendlyName = ref(props.model?.name);
const exportText = ref("");

const buildExport = (): void => {
  const playlists = props.model?.playlists;
  const id = props.model?.id;

  if (!playlists || !id) return;

  const rows = playlists
    .filter((p) => !p.music4danceId && p.owner === id)
    .map(
      (p) =>
        `${friendlyName.value}\t${p.name}\thttps://open.spotify.com/playlist/${p.id}\t${id}\t${p.trackCount}\t${p.description}`,
    );

  exportText.value = playlistHeader + "\r\n" + rows.join("\r\n");
};
</script>

<template>
  <div>
    <h2>{{ model?.name }}</h2>
    <p>id = ({{ model?.id }}), count = {{ model?.playlists?.length ?? 0 }}</p>
    <SimplePlaylists :playlists="model?.playlists || []"></SimplePlaylists>

    <BInputGroup v-if="context.isAdmin">
      <label class="sr-only" for="service-id">Friendly Name:</label>
      <BFormInput
        id="friendly-name"
        v-model="friendlyName"
        palceholder="UserName"
        aria-describedby="service-id-feedback"
        trim
        class="me-2"
        @input="buildExport"
      ></BFormInput>
      <BButton variant="primary" @click="buildExport">Build Exports</BButton>
    </BInputGroup>
    <BFormTextarea id="export" v-model="exportText" rows="10" max-rows="100"></BFormTextarea>
  </div>
</template>
