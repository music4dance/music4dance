<script setup lang="ts">
defineProps<{
  active: string;
}>();
</script>

<template>
  <BNav
    ><SearchNavItem id="song-results" title="Songs" :active="active" /><SearchNavItem
      id="page-results"
      title="Pages"
      :active="active" /><SearchNavItem
      id="blog-results"
      title="Blog Posts"
      :active="active" /><SearchNavItem id="help-results" title="Help Pages" :active="active"
  /></BNav>
</template>
