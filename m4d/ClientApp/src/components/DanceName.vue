<script setup lang="ts">
import { TempoType } from "@/models/TempoType";
import { TypeStats } from "@/models/TypeStats";
import type { NamedObject } from "@/models/NamedObject";
import { computed } from "vue";
import { DanceGroup } from "@/models/DanceGroup";

const props = withDefaults(
  defineProps<{
    dance: NamedObject;
    showTempo?: TempoType;
    showSynonyms?: boolean;
    multiLine?: boolean;
    hideLink?: boolean;
  }>(),
  {
    showTempo: TempoType.None,
    showSynonyms: false,
    multiLine: false,
    hideLink: false,
  },
);

const danceLink = computed(() => {
  return `/dances/${props.dance.seoName}`;
});

const canShowTempo = computed(() => {
  const dance = DanceGroup.isGroup(props.dance) ? undefined : (props.dance as TypeStats);
  return !!dance && !dance.tempoRange.isInfinite;
});

const tempoText = computed(() => {
  const dance = DanceGroup.isGroup(props.dance) ? undefined : (props.dance as TypeStats);
  if (!dance) return "";

  const showTempo = props.showTempo;
  const bpm = showTempo & TempoType.Beats ? `${dance.tempoRange.toString()} BPM` : "";
  const mpm =
    showTempo & TempoType.Measures ? `${dance.tempoRange.mpm(dance.meter.numerator)} MPM` : "";

  return `${bpm}${mpm && bpm ? "/" : ""}${mpm}`;
});

const synonymText = computed(() => {
  const synonyms = props.dance.synonyms;
  return props.showSynonyms && synonyms ? `${synonyms.join(", ")}` : "";
});
</script>

<template>
  <span
    ><span v-if="hideLink">{{ dance.name }}</span
    ><a v-else :href="danceLink">{{ dance.name }}</a>
    <span v-if="synonymText"
      ><br v-if="multiLine" />
      ({{ synonymText }})
    </span>
    <span v-if="showTempo && canShowTempo" style="font-size: 0.8rem" class="ms-2">
      {{ tempoText }}</span
    >
  </span>
</template>
