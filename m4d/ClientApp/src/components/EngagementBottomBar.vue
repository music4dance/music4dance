<template>
  <BAlert
    variant="light"
    class="engagement-bottom-bar mb-0 rounded-0 border-top border-start-0 border-end-0 border-bottom-0"
    :model-value="true"
    :dismissible="false"
    @click="onExpand"
    role="button"
    tabindex="0"
    @keydown.enter="onExpand"
    @keydown.space.prevent="onExpand"
    aria-label="Expand engagement options"
  >
    <div class="d-flex align-items-center">
      <IBiChevronUp class="me-3" />
      <span class="small">How to support music4dance</span>
    </div>
  </BAlert>
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
  cursor: pointer;
  z-index: 1030; // Below modals (1055), above nav (1020)
  transition: opacity 0.2s;

  &:hover {
    opacity: 0.9;
  }

  &:focus-visible {
    outline: 2px solid var(--bs-primary);
    outline-offset: -2px;
  }
}
</style>
