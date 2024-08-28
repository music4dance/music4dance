<script setup lang="ts">
import { type FeatureInfo } from "@/models/FeatureInfo";

const info: FeatureInfo = {
  title:
    "Explore relationships between the meter and tempi of different dance styles and find songs that match.",
  type: "tools",
  tryIt: "https://www.music4dance.net/home/tempi",
  docs: "https://music4dance.blog/music4dance-help/dance-tempi/",
  posts: "https://music4dance.blog/tag/tempo/",
};
</script>

<template>
  <FeatureLink :info="info" />
</template>
