<script setup lang="ts">
const slots = defineSlots<{
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  default?: (props: Record<string, never>) => any;
}>();

const props = defineProps<{
  title: string;
  link: string;
  summary?: string;
}>();
</script>

<template>
  <BListGroupItem class="d-flex justify-content-between align-items-start">
    <div class="ms-2 me-auto">
      <BLink :href="link" class="fw-bold fs-5" target="_blank" underline-opacity="0"
        >{{ title }} <IBiBoxArrowUpRight
      /></BLink>
      <div class="mt-1">
        <slot>{{ summary }}</slot>
      </div>
    </div>
  </BListGroupItem>
</template>
