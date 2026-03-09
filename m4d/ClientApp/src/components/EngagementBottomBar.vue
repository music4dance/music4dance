<template>
  <div
    class="engagement-bottom-bar"
    @click="onExpand"
    role="button"
    tabindex="0"
    @keydown.enter="onExpand"
    @keydown.space.prevent="onExpand"
    aria-label="Expand engagement options"
  >
    <div class="container-fluid d-flex justify-content-between align-items-center py-2">
      <span class="text-muted small">How to support music4dance</span>
      <IBiChevronUp class="text-muted" />
    </div>
  </div>
</template>

<script setup lang="ts">
interface Emits {
  (event: "expand"): void;
}

const emit = defineEmits<Emits>();

function onExpand() {
  emit("expand");
}
</script>

<style scoped lang="scss">
.engagement-bottom-bar {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  height: 40px;
  background-color: var(--bs-light);
  border-top: 1px solid var(--bs-border-color);
  cursor: pointer;
  transition: background-color 0.2s;
  z-index: 1030; // Below modals (1055), above nav (1020)

  &:hover {
    background-color: var(--bs-gray-200);
  }

  &:focus {
    outline: 2px solid var(--bs-primary);
    outline-offset: -2px;
  }
}
</style>
