<script setup lang="ts">
import { TempoType } from "@/models/DanceDatabase/TempoType";
import { DanceOrder } from "@/models/DanceDatabase/DanceOrder";
import { computed } from "vue";
import type { DeltaMetric } from "../deltaMetrics";

const props = defineProps<{
  dance: DanceOrder;
  tempoType: number;
  hideLink: boolean;
  metrics: DeltaMetric[];
}>();

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

// Signed the way a pitch fader is labeled: "−" slows the song down, "+" speeds it up.
const pitchMessage = computed(() => {
  const pitch = props.dance.pitchPercent;
  return "Pitch " + (pitch < 0 ? "−" : "+") + Math.abs(pitch).toFixed(1) + "%";
});

const speedMessage = computed(() => "Speed " + props.dance.speedPercent.toFixed(1) + "%");

const badges = computed(() =>
  [
    { key: "tempo", text: deltaMessage.value, title: undefined },
    {
      key: "pitch",
      text: pitchMessage.value,
      title:
        "Set your CD player's or DJ deck's pitch (tempo) control to this to play the song at this dance's tempo",
    },
    {
      key: "speed",
      text: speedMessage.value,
      title:
        "Set the playback speed in tempo-change software to this to play the song at this dance's tempo (100% = unchanged)",
    },
  ].filter((b) => props.metrics.includes(b.key as DeltaMetric)),
);
</script>

<template>
  <BListGroupItem
    :variant="variant"
    href="#"
    class="d-flex justify-content-between align-items-center"
    @click.prevent="emit('choose-dance', dance.dance.id, $event.ctrlKey)"
  >
    <DanceName
      :dance="dance.dance"
      :show-tempo="tempoType"
      :show-synonyms="true"
      :show-meter="true"
      :hide-link="hideLink"
    />
    <span v-show="showDelta" class="d-flex flex-wrap justify-content-end gap-1">
      <BBadge v-for="badge in badges" :key="badge.key" :variant="variant" :title="badge.title">{{
        badge.text
      }}</BBadge>
    </span>
  </BListGroupItem>
</template>
