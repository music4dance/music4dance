<script setup lang="ts">
import { TempoType } from "@/models/DanceDatabase/TempoType";
import { DanceOrder } from "@/models/DanceDatabase/DanceOrder";
import { computed } from "vue";

const props = defineProps<{ dance: DanceOrder; tempoType: number; hideLink: boolean }>();

const emit = defineEmits<{ "choose-dance": [danceId: string, ctrl: boolean] }>();
const variant = computed(() => {
  if (!showDelta.value) {
    return "primary";
  }

  return props.dance.deltaMpm < 0 ? "warning" : "success";
});

const showDelta = computed(() => {
  return Math.abs(props.dance.deltaMpm) >= 1.0;
});

const deltaMessage = computed(() => {
  const measures = props.tempoType === TempoType.Measures;
  const slower = props.dance.delta < 0;
  const abs = Math.abs(measures ? props.dance.deltaMpm : props.dance.delta).toFixed(1);
  return abs + " " + (measures ? "M" : "B") + "PM " + (slower ? "slow" : "fast");
});
</script>

<template>
  <BListGroupItem
    :variant="variant"
    href="#"
    class="d-flex justify-content-between align-items-center"
    @click="emit('choose-dance', dance.dance.id, $event.ctrlKey)"
  >
    <DanceName
      :dance="dance.dance"
      :show-tempo="tempoType"
      :show-synonyms="true"
      :hide-link="hideLink"
    ></DanceName>
    <BBadge v-show="showDelta" :variant="variant">{{ deltaMessage }}</BBadge>
  </BListGroupItem>
</template>
