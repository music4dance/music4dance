<template>
  <a :href="link">{{ name }}</a>
</template>

<script lang="ts">
import { wordsToKebab } from "@/helpers/StringHelpers";
import "reflect-metadata";
import Vue from "vue";

export default Vue.extend({
  components: {},
  props: { name: { type: String, required: true } },
  computed: {
    link(): string {
      return `/dances/${wordsToKebab(this.name)}`;
    },
  },
});
</script>
