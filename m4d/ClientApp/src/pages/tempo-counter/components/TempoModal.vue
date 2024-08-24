<script setup lang="ts">
import { useModal } from "bootstrap-vue-next";
import { ref } from "vue";

const input = ref<HTMLElement | null>(null);

const props = defineProps<{
  id: string;
  label: string;
  tempo: number;
}>();

const emit = defineEmits<{
  "change-tempo": [value: number];
}>();

const internalTempo = ref("0.0");

const { hide } = useModal(props.id);

function submit(): void {
  emit("change-tempo", Number(internalTempo.value));
  hide();
}

function show(): void {
  internalTempo.value = props.tempo.toFixed(1);
}

function focus(): void {
  input.value?.focus();
}
</script>

<template>
  <BModal :id="id" title="Set Tempo" size="sm" @ok="submit" @show="show" @shown="focus">
    <form ref="form" @submit.stop.prevent="submit">
      <BFormGroup :label="label" label-for="tempo-input">
        <BFormInput
          id="tempo-input"
          ref="input"
          v-model="internalTempo"
          type="number"
          step=".1"
          min="0"
          max="500"
        />
      </BFormGroup>
    </form>
  </BModal>
</template>
