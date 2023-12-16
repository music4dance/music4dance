<script setup lang="ts">
const props = withDefaults(defineProps<{ loaded: boolean; label?: string }>(), {
  loaded: true,
  label: "Loading...",
});
</script>

<template>
  <div>
    <slot v-if="loaded"></slot>
    <div v-else class="d-flex justify-content-center mb-3">
      <b-spinner :label="props.label"></b-spinner>
    </div>
  </div>
</template>
