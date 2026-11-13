<script setup lang="ts">
import DOMPurify from "dompurify";
import { marked } from "marked";
import { computed } from "vue";

const props = defineProps<{ markdown: string }>();

// Must be computed: the description editor changes `markdown` as the admin types.
const html = computed(() =>
  DOMPurify.sanitize(marked.parse(props.markdown, { async: false, gfm: true })),
);
</script>

<template>
  <!-- eslint-disable-next-line vue/no-v-html -- output is sanitized by DOMPurify -->
  <div v-html="html" />
</template>
