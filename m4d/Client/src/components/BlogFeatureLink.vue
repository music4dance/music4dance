<template>
  <div v-if="!entry.oneTime" style="margin-left: 2em">
    <div class="info">
      <img src="/images/icons/info.png" alt="info" width="24" height="24" />
      {{ entry.title }}
    </div>
    <div style="margin-left: 2em">
      <span v-html="entry.description"></span>
      <a :href="entry.fullPath" style="margin-left: 0.5em">[Read more]</a>
    </div>
  </div>
</template>

<script lang="ts">
import { SiteMapEntry } from "@/model/SiteMapInfo";
import "reflect-metadata";
import Vue, { PropType } from "vue";

export default Vue.extend({
  props: {
    entry: {
      type: Object as PropType<SiteMapEntry>,
      required: true,
    },
  },
});
</script>
