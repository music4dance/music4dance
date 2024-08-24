<script setup lang="ts">
const props = defineProps<{
  loaded: boolean;
  placeholder?: string;
}>();

const label = props.placeholder ?? "Loading...";
</script>

<template>
  <div>
    <slot v-if="loaded" />
    <div v-else class="d-flex justify-content-center mb-3">
      <BSpinner :label="label" />
    </div>
  </div>
</template>
