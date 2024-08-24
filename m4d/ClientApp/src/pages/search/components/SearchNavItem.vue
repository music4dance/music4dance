<script setup lang="ts">
const props = defineProps<{
  id: string;
  title: string;
  active: string;
}>();

const isActive = props.active === props.id;
const onClick = (): void => {
  window.location.hash = `#${props.id}`;
};
</script>

<template>
  <BNav pills
    ><BNavItem :active="isActive" @click="onClick">{{ title }}</BNavItem></BNav
  >
</template>
