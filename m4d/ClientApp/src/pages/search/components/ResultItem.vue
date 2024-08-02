<script setup lang="ts">
import type { SearchPage } from "@/models/SearchPage";

defineProps<{
  entry: SearchPage;
}>();
</script>

<template>
  <div class="mb-1">
    <div><a :href="entry.url" class="title" v-html="entry.title"></a></div>
    <div class="link">
      <a :href="entry.url">{{ entry.url }}</a>
    </div>
    <div v-html="entry.description"></div>
  </div>
</template>

<style lang="scss" scoped>
.title {
  font-size: 1.5rem;
  font-weight: bold;
  line-height: 1.1rem;
}
.text {
  width: auto;
}
.link {
  margin-top: -0.25rem;
}
</style>
