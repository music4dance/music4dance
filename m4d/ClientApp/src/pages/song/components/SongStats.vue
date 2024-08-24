<script setup lang="ts">
import { Song } from "@/models/Song";
import { SongProperty } from "@/models/SongProperty";
import { computed } from "vue";

defineOptions({ inheritAttrs: false });

const props = defineProps<{
  song: Song;
  editing?: boolean;
  isCreator?: boolean;
}>();

const modifiedFormatted = computed(() => SongProperty.formatDate(props.song.modified));
const createdFormatted = computed(() => SongProperty.formatDate(props.song.created));
const editedFormatted = computed(() => SongProperty.formatDate(props.song.edited!));

const formatEchoNest = (n: number): string => {
  return (n * 100).toFixed(1).toString() + "%";
};
</script>

<template>
  <BTableSimple borderless small>
    <BTr v-if="!!song.length || editing">
      <BTh>Length</BTh>
      <BTd
        ><FieldEditor
          name="Length"
          :value="song.length ? song.length.toString() : ''"
          :editing="editing"
          :is-creator="isCreator"
          role="isAdmin"
          type="number"
          v-bind="$attrs"
        />
        Seconds</BTd
      >
    </BTr>
    <BTr v-if="!!song.tempo || editing">
      <BTh>Tempo</BTh>
      <BTd
        ><FieldEditor
          name="Tempo"
          :value="song.tempo ? song.tempo.toString() : ''"
          :editing="editing"
          :is-creator="isCreator"
          role="canTag"
          type="number"
          v-bind="$attrs"
        />
        BPM</BTd
      >
    </BTr>
    <BTr v-if="song.danceability">
      <BTh>Beat</BTh>
      <BTd>
        <EchoIcon
          :value="song.danceability"
          type="beat"
          label="beat strength"
          max-label="strongest beat"
        />
        {{ formatEchoNest(song.danceability) }}</BTd
      >
    </BTr>
    <BTr v-if="song.energy">
      <BTh>Energy</BTh>
      <BTd>
        <EchoIcon
          :value="song.energy"
          type="energy"
          label="energy level"
          max-label="highest energy"
        />
        {{ formatEchoNest(song.energy) }}</BTd
      >
    </BTr>
    <BTr v-if="song.valence">
      <BTh>Mood</BTh>
      <BTd>
        <EchoIcon :value="song.valence" type="mood" label="mood level" max-label="happiest" />
        {{ formatEchoNest(song.valence) }}</BTd
      >
    </BTr>
    <BTr v-if="song.created">
      <BTh>Created</BTh>
      <BTd>{{ createdFormatted }}</BTd>
    </BTr>
    <BTr v-if="song.modified">
      <BTh>Modified</BTh>
      <BTd>{{ modifiedFormatted }}</BTd>
    </BTr>
    <BTr v-if="song.edited">
      <BTh>Edited</BTh>
      <BTd>{{ editedFormatted }}</BTd>
    </BTr>
  </BTableSimple>
</template>
