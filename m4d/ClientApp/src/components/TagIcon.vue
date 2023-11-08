<script setup lang="ts">
// INT-TODO: This is a hack to get around the new icon system not not taking a name parameter
defineProps<{ name: string }>();
</script>

<template>
  <IBiBriefcase v-if="name === 'briefcase'" />
  <IBiClock v-else-if="name === 'clock'" />
  <IBiMusicNoteList v-else-if="name === 'music-note-list'" />
  <IBiTag v-else-if="name === 'tag'" />
  <IBiAward v-else-if="name == 'award'" />
</template>
