<script setup lang="ts">
defineProps<{
  type: string;
  direction: string;
}>();
</script>

<template>
  <IBiSortNumericDown v-if="type === 'numeric' && direction === 'asc'" />
  <IBiSortNumericDownAlt v-if="type === 'numeric' && direction === 'desc'" />
  <IBiSortAlphaDown v-if="type === 'alpha' && direction === 'asc'" />
  <IBiSortAlphaDownAlt v-if="type === 'alpha' && direction === 'desc'" />
  <IBiSortDown v-if="type === '' && direction === 'asc'" />
  <IBiSortDownAlt v-if="type === '' && direction === 'desc'" />
</template>
