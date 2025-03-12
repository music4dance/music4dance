<script setup lang="ts">
const props = withDefaults(defineProps<{ loaded?: boolean; label?: string }>(), {
  loaded: true,
  label: "Loading...",
});
</script>

<template>
  <div>
    <slot v-if="loaded" />
    <div v-else class="d-flex justify-content-center mb-3">
      <BSpinner :label="props.label" />
    </div>
  </div>
</template>
