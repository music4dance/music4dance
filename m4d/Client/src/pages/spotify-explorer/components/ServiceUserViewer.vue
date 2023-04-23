<template>
  <div>
    <h2>{{ model?.name }}</h2>
    <p>id = ({{ model?.id }}), count = {{ model?.playlists?.length ?? 0 }}</p>
    <simple-playlists :playlists="model?.playlists"></simple-playlists>
  </div>
</template>

<script lang="ts">
import Vue, { PropType } from "vue";
import SimplePlaylists from "./SimplePlaylists.vue";
import { ServiceUser } from "@/model/ServiceUser";

export default Vue.extend({
  components: {
    SimplePlaylists,
  },
  props: {
    model: Object as PropType<ServiceUser | null>,
  },
});
</script>
