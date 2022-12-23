<template>
  <p>
    <a :href="danceLink">Browse</a> all <b>{{ danceCount }}</b>
    {{ dance.danceName }} songs in the <a href="/">music4dance</a
    ><span> </span> <a href="/song">catalog</a>.
  </p>
</template>

<script lang="ts">
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceStats } from "@/model/DanceStats";
import "reflect-metadata";

export default EnvironmentManager.extend({
  components: {},
  props: { danceId: String },
  computed: {
    dance(): DanceStats | undefined {
      const id = this.danceId;
      return id ? this.environment.fromId(id) : undefined;
    },

    danceLink(): string | undefined {
      const id = this.danceId;
      return id ? `/song/search?dances=${id}` : undefined;
    },

    danceCount(): number | undefined {
      return this.dance?.songCount;
    },
  },
});
</script>
