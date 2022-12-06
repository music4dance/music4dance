<template>
  <div class="mb-1">
    <div><a v-html="entry.title" :href="entry.url" class="title"></a></div>
    <div class="link">
      <a :href="entry.url">{{ entry.url }}</a>
    </div>
    <div v-html="entry.description"></div>
  </div>
</template>

<script lang="ts">
import type { SearchPage } from "@/model/SearchPage";
import "reflect-metadata";
import Vue, { PropType } from "vue";

export default Vue.extend({
  props: {
    entry: { type: Object as PropType<SearchPage>, required: true },
  },
});
</script>

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
