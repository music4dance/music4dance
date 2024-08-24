<script setup lang="ts">
const model = defineModel<number>({ required: true });
</script>

<template>
  <div style="margin-bottom: 1rem">
    <BButtonGroup>
      <BButton variant="primary">BPM</BButton>
      <BButton v-b-modal.bpm-modal variant="outline-primary">{{ modelValue.toFixed(1) }}</BButton>
    </BButtonGroup>
    <TempoModal
      id="bpm-modal"
      label="Beats Per Minute"
      :tempo="model"
      @change-tempo="model = $event"
    />
  </div>
</template>
