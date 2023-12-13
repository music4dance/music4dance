<script setup lang="ts">
import type { FeatureInfo } from "@/models/FeatureInfo";
import FeatureButton from "./FeatureButton.vue";
import { computed } from "vue";

const props = defineProps<{
  info: FeatureInfo;
}>();

const icon = computed(() => {
  return `/images/icons/${props.info.type}.png`;
});

const pathItems = computed(() => {
  const menu = props.info.menu;
  return menu ? menu.filter((s, i) => i != menu.length - 1) : [];
});

const actionItem = computed(() => {
  const menu = props.info.menu;
  return menu ? menu[menu.length - 1] : "";
});
</script>

<template>
  <div>
    <div>
      <img :src="icon" :alt="info.type" width="24" height="24" />
      {{ info.title }}
    </div>
    <div class="ms-3 my-1">
      <FeatureButton
        title="Try It!"
        type="play"
        :variant="info.type"
        :link="info.tryIt"
        style="margin-bottom: 0.25em"
      />
      <FeatureButton
        title="Documentation"
        type="docs"
        :variant="info.type"
        :link="info.docs"
        style="margin-bottom: 0.25em"
      />
      <FeatureButton
        title="Blog Posts"
        type="blog"
        :variant="info.type"
        :link="info.posts"
        style="margin-bottom: 0.25em"
      />
    </div>
    <div v-if="info.menu" class="ms-5">
      Menu Location:
      <span v-for="item in pathItems" :key="item">{{ item }} / </span>
      <a :href="info.tryIt">{{ actionItem }}</a>
    </div>
  </div>
</template>
