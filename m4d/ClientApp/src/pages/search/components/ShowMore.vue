<script setup lang="ts">
defineProps<{
  extraId: string;
}>();
const model = defineModel<boolean>();
</script>

<template>
  <div class="d-grid gap-2">
    <BButton
      variant="outline-primary"
      :aria-expanded="model ? 'true' : 'false'"
      :aria-controls="extraId"
      @click="model = !model"
    >
      <span v-if="model"><IBiCaretUp class="mx-3" />Show Less<IBiCaretUp class="mx-3" /></span>
      <span v-else><IBiCaretDown class="mx-3" />Show More<IBiCaretDown class="mx-3" /></span>
    </BButton>
  </div>
</template>
