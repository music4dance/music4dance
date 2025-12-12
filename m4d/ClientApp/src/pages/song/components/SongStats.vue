<script setup lang="ts">
import { Song } from "@/models/Song";
import { PropertyType } from "@/models/SongProperty";
import { computed } from "vue";
import { formatDate } from "@/helpers/timeHelpers";

defineOptions({ inheritAttrs: false });

const props = defineProps<{
  song: Song;
  editing?: boolean;
  isCreator?: boolean;
}>();

const modifiedFormatted = computed(() => formatDate(props.song.modified));
const createdFormatted = computed(() => formatDate(props.song.created));
const editedFormatted = computed(() => formatDate(props.song.edited!));

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
          v-bind="$attrs" />
        BPM<a
          v-if="song.tempo && !song.isUserModified(PropertyType.tempoField) && !editing"
          href="https://music4dance.blog/music4dance-help/song-list/#tempo-note"
          target="_blank"
          title="This tempo was algorithmically generated. Click to learn more."
          class="algo-generated-icon"
        >
          <IBiCpuFill /> </a
      ></BTd>
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

<style scoped>
.algo-generated-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.8em;
  height: 1.8em;
  margin-left: 0.5em;
  border-radius: 50%;
  background-color: rgba(108, 117, 125, 0.1);
  border: 1px solid rgba(108, 117, 125, 0.2);
  text-decoration: none;
  transition: all 0.2s ease-in-out;
  cursor: pointer;
}

.algo-generated-icon:hover {
  background-color: rgba(108, 117, 125, 0.2);
  border-color: rgba(108, 117, 125, 0.4);
  transform: scale(1.1);
}

.algo-generated-icon svg {
  width: 1em;
  height: 1em;
  color: #6c757d;
}

.algo-generated-icon:hover svg {
  color: #495057;
}
</style>
