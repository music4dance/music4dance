<script setup lang="ts">
import { TempoType } from "@/models/DanceDatabase/TempoType";
import { DanceType } from "@/models/DanceDatabase/DanceType";
import type { NamedObject } from "@/models/DanceDatabase/NamedObject";
import { computed } from "vue";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";

const props = withDefaults(
  defineProps<{
    dance: NamedObject;
    showTempo?: TempoType;
    showSynonyms?: boolean;
    multiLine?: boolean;
    hideLink?: boolean;
    showBlogLink?: boolean;
  }>(),
  {
    showTempo: TempoType.None,
    showSynonyms: false,
    multiLine: false,
    hideLink: false,
    showBlogLink: false,
  },
);

const danceLink = computed(() => {
  return `/dances/${props.dance.seoName}`;
});

// blogTag lives on both DanceObject (DanceType/DanceInstance) and DanceGroup independently -
// NamedObject itself doesn't declare it, so narrow with a cast rather than importing both types.
const blogLink = computed(() => {
  const blogTag = (props.dance as { blogTag?: string }).blogTag;
  return blogTag ? `https://music4dance.blog/tag/${blogTag}` : undefined;
});

const canShowTempo = computed(() => {
  const dance = DanceGroup.isGroup(props.dance) ? undefined : (props.dance as DanceType);
  return !!dance && !dance.tempoRange.isInfinite;
});

const tempoText = computed(() => {
  const dance = DanceGroup.isGroup(props.dance) ? undefined : (props.dance as DanceType);
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

// Exposed for testing
defineExpose({ danceLink, blogLink, canShowTempo, tempoText, synonymText });
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
    ><a
      v-if="showBlogLink && blogLink"
      :href="blogLink"
      target="_blank"
      rel="noopener noreferrer"
      title="Blog Posts"
      class="ms-1"
      ><IBiNewspaper
    /></a>
  </span>
</template>
