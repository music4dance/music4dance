<script setup lang="ts">
defineProps<{
  link: string;
  text?: string;
  styles?: string | string[] | Record<string, string>;
}>();
</script>

<template>
  <a :href="link" :style="styles"
    ><slot>{{ text }}</slot></a
  >
</template>
