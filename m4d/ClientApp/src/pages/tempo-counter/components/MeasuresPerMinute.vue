<script setup lang="ts">
import { computed } from "vue";

const model = defineModel<number>({ required: true });
const beatsPerMeasure = defineModel<number>("beatsPerMeasure", { required: true });

const meterDescription = computed(() => {
  return "MPM (" + beatsPerMeasure.value + "/4)";
});
</script>

<template>
  <div style="margin-bottom: 1rem">
    <BButtonGroup>
      <BDropdown id="meter-selector" :text="meterDescription" variant="primary">
        <BDropdownItem v-show="beatsPerMeasure !== 2" @click="beatsPerMeasure = 2"
          >MPM 2/4</BDropdownItem
        >
        <BDropdownItem v-show="beatsPerMeasure !== 3" @click="beatsPerMeasure = 3"
          >MPM 3/4</BDropdownItem
        >
        <BDropdownItem v-show="beatsPerMeasure !== 4" @click="beatsPerMeasure = 4"
          >MPM 4/4</BDropdownItem
        >
      </BDropdown>
      <BButton v-b-modal.mpm-modal variant="outline-primary">{{ model.toFixed(1) }}</BButton>
    </BButtonGroup>
    <TempoModal
      id="mpm-modal"
      label="Measures Per Minute"
      :tempo="model"
      @change-tempo="model = $event"
    />
  </div>
</template>
