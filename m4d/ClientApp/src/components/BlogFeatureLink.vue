<script setup lang="ts">
import { SiteMapEntry } from "@/models/SiteMapInfo";
import info from "@/assets/images/icons/info.png";
import { format, parseISO } from "date-fns";

defineProps<{
  entry: SiteMapEntry;
}>();

function formatEntryDate(date?: string): string {
  return date ? format(parseISO(date), "MMM d, yyyy") : "";
}
</script>

<template>
  <div v-if="!entry.oneTime" style="margin-left: 2em">
    <div class="info">
      <img :src="info" alt="info" width="24" height="24" />
      {{ entry.title }}<span v-if="entry.date"> ({{ formatEntryDate(entry.date) }})</span>
    </div>
    <div style="margin-left: 2em">
      <span v-html="entry.description" />
      <a :href="entry.fullPath" style="margin-left: 0.5em">[Read more]</a>
    </div>
    <div v-if="entry.children">
      <BlogFeatureLink v-for="child in entry.children" :key="child.reference" :entry="child" />
    </div>
  </div>
</template>
