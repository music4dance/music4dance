<script setup lang="ts">
withDefaults(
  defineProps<{
    state?: boolean;
    scale?: number;
  }>(),
  {
    scale: 1,
  },
);
</script>

<template>
  <IBiHeartFill v-if="state" :style="{ color: 'red', fontSize: scale + 'em' }" />
  <IBiHeartbreakFill
    v-else-if="state === false"
    :style="{ color: 'black', fontSize: scale + 'em' }"
  />
  <IBiHeart v-else :style="{ color: 'black', fontSize: scale + 'em' }" />
</template>
