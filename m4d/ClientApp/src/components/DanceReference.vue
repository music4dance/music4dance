<script setup lang="ts">
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import { computed } from "vue";

const props = defineProps<{
  danceId: string;
}>();
const danceDB = safeDanceDatabase();
const dance = computed(() => danceDB.fromId(props.danceId));
const danceLink = computed(() =>
  dance.value ? `/song/search?dances=${props.danceId}` : undefined,
);
const isGroup = computed(() => dance.value && DanceGroup.isGroup(dance.value));
const songCount = computed(() => (isGroup.value ? 0 : danceDB.getSongCount(props.danceId)));
</script>

<template>
  <p>
    <a :href="danceLink">Browse</a> all <b v-if="!isGroup">{{ songCount }}</b>
    {{ dance!.name }} songs in the <a href="/">music4dance</a><span> </span>
    <a href="/song">catalog</a>.
  </p>
</template>
