<template>
  <feature-link :info="info"></feature-link>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Vue } from "vue-property-decorator";
import FeatureLink from "./FeatureLink.vue";
import { FeatureInfo } from "@/model/FeatureInfo";

@Component({
  components: {
    FeatureLink,
  },
})
export default class TempiLink extends Vue {
  private info: FeatureInfo = {
    title:
      "Explore relationships between the meter and tempi of different dance styles and find songs that match.",
    type: "tools",
    tryIt: "https://www.music4dance.net/home/tempi",
    docs: "https://music4dance.blog/music4dance-help/dance-tempi/",
    posts: "https://music4dance.blog/tag/tempo/",
  };
}
</script>
