<script setup lang="ts">
const model = defineModel<number>({ required: true });
</script>

<template>
  <div style="margin-bottom: 1rem; width: 250px" class="mx-auto">
    <BFormInput
      id="strictness"
      v-model="model"
      type="range"
      min="0"
      max="20"
      step="0.5"
      number
    ></BFormInput>
    <label for="strictness" class="d-flex justify-content-between"
      ><span>less</span><span class="ml-auto">more</span></label
    >
  </div>
</template>
