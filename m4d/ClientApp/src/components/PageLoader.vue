<script setup lang="ts">
const props = withDefaults(defineProps<{ loaded: boolean; label?: string }>(), {
  loaded: true,
  message: "Loading...",
});
</script>

<template>
  <div>
    <slot v-if="loaded"></slot>
    <div v-else class="d-flex justify-content-center mb-3">
      <b-spinner :label="props.message"></b-spinner>
    </div>
  </div>
</template>
