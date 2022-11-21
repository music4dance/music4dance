<template>
  <feature-link :info="info"></feature-link>
</template>

<script lang="ts">
import { FeatureInfo } from "@/model/FeatureInfo";
import "reflect-metadata";
import Vue from "vue";
import FeatureLink from "./FeatureLink.vue";

export default Vue.extend({
  components: {
    FeatureLink,
  },
  computed: {
    info(): FeatureInfo {
      return {
        title:
          "Explore relationships between the meter and tempi of different dance styles and find songs that match.",
        type: "tools",
        tryIt: "https://www.music4dance.net/home/tempi",
        docs: "https://music4dance.blog/music4dance-help/dance-tempi/",
        posts: "https://music4dance.blog/tag/tempo/",
      };
    },
  },
});
</script>
