<script setup lang="ts">
import { SiteMapEntry } from "@/models/SiteMapInfo";
import info from "@/assets/images/icons/info.png";

defineProps<{
  entry: SiteMapEntry;
}>();
</script>

<template>
  <div v-if="!entry.oneTime" style="margin-left: 2em">
    <div class="info">
      <img :src="info" alt="info" width="24" height="24" />
      {{ entry.title }}
    </div>
    <div style="margin-left: 2em">
      <span v-html="entry.description"></span>
      <a :href="entry.fullPath" style="margin-left: 0.5em">[Read more]</a>
    </div>
    <div v-if="entry.children">
      <blog-feature-link
        v-for="child in entry.children"
        :key="child.order"
        :entry="child"
      ></blog-feature-link>
    </div>
  </div>
</template>
