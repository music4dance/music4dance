<script setup lang="ts">
defineSlots<{
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  summary?: (props: Record<string, never>) => any;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  default?: (props: Record<string, never>) => any;
}>();

defineProps<{
  title: string;
  summary?: string;
}>();
</script>

<template>
  <div class="mt-3">
    <H2>{{ title }}</H2>
    <p>
      <slot name="summary">{{ summary }}</slot>
    </p>
    <BListGroup>
      <slot />
    </BListGroup>
  </div>
</template>
