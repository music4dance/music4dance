<script setup lang="ts">
withDefaults(
  defineProps<{
    id?: string;
    placeholder?: string;
  }>(),
  { id: "name-filter", placeholder: "Filter Dances" },
);

const modelValue = defineModel<string>({ required: true });
</script>

<template>
  <BInputGroup class="mb-2">
    <BFormInput :id="id" v-model="modelValue" type="text" :placeholder="placeholder" autofocus />
    <BInputGroupText><IBiSearch /></BInputGroupText>
  </BInputGroup>
</template>
