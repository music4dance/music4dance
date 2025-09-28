<script setup lang="ts">
defineSlots<{
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  summary?: (props: Record<string, never>) => any;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  default?: (props: Record<string, never>) => any;
}>();

defineProps<{
  title: string;
  link?: string;
  summary?: string;
}>();
</script>

<template>
  <div class="mt-3">
    <h2>
      <BLink v-if="link" :href="link" target="_blank" underline-opacity="0"
        >{{ title }} <IBiBoxArrowUpRight /></BLink
      ><span v-else>{{ title }}</span>
    </h2>
    <p>
      <slot name="summary">{{ summary }}</slot>
    </p>
    <BListGroup>
      <slot />
    </BListGroup>
  </div>
</template>
