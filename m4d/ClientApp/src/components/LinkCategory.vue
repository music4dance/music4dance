<script setup lang="ts">
import { wordsToKebab } from "@/helpers/StringHelpers";
import { computed } from "vue";

const props = defineProps<{ name: string }>();
const link = computed(() => `/dances/${wordsToKebab(props.name)}`);
</script>

<template>
  <a :href="link">{{ name }}</a>
</template>
