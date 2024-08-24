<script setup lang="ts">
defineProps<{ name: string }>();
</script>

<template>
  <IBiBriefcase v-if="name === 'briefcase'" />
  <IBiClock v-else-if="name === 'clock'" />
  <IBiMusicNoteList v-else-if="name === 'music-note-list'" />
  <IBiTag v-else-if="name === 'tag'" />
  <IBiAward v-else-if="name == 'award'" />
</template>
