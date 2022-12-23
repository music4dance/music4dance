<template>
  <b-table-simple borderless small>
    <b-tr v-if="!!song.length || editing">
      <b-th>Length</b-th>
      <b-td
        ><field-editor
          name="Length"
          :value="song.length ? song.length.toString() : ''"
          :editing="editing"
          :isCreator="isCreator"
          role="isAdmin"
          type="number"
          v-on="$listeners"
        ></field-editor>
        Seconds</b-td
      >
    </b-tr>
    <b-tr v-if="!!song.tempo || editing">
      <b-th>Tempo</b-th>
      <b-td
        ><field-editor
          name="Tempo"
          :value="song.tempo ? song.tempo.toString() : ''"
          :editing="editing"
          :isCreator="isCreator"
          role="canTag"
          type="number"
          v-on="$listeners"
        ></field-editor>
        BPM</b-td
      >
    </b-tr>
    <b-tr v-if="song.danceability">
      <b-th>Beat</b-th>
      <b-td>
        <echo-icon
          :value="song.danceability"
          type="beat"
          label="beat strength"
          maxLabel="strongest beat"
        ></echo-icon>
        {{ formatEchoNest(song.danceability) }}</b-td
      >
    </b-tr>
    <b-tr v-if="song.energy">
      <b-th>Energy</b-th>
      <b-td>
        <echo-icon
          :value="song.energy"
          type="energy"
          label="energy level"
          maxLabel="highest energy"
        ></echo-icon>
        {{ formatEchoNest(song.energy) }}</b-td
      >
    </b-tr>
    <b-tr v-if="song.valence">
      <b-th>Mood</b-th>
      <b-td>
        <echo-icon
          :value="song.valence"
          type="mood"
          label="mood level"
          maxLabel="happiest"
        ></echo-icon>
        {{ formatEchoNest(song.valence) }}</b-td
      >
    </b-tr>
    <b-tr v-if="song.created">
      <b-th>Created</b-th>
      <b-td>{{ createdFormatted }}</b-td>
    </b-tr>
    <b-tr v-if="song.modified">
      <b-th>Modified</b-th>
      <b-td>{{ modifiedFormatted }}</b-td>
    </b-tr>
    <b-tr v-if="song.edited">
      <b-th>Edited</b-th>
      <b-td>{{ editedFormatted }}</b-td>
    </b-tr>
  </b-table-simple>
</template>

<script lang="ts">
import EchoIcon from "@/components/EchoIcon.vue";
import { Song } from "@/model/Song";
import { SongProperty } from "@/model/SongProperty";
import "reflect-metadata";
import Vue, { PropType } from "vue";
import FieldEditor from "./FieldEditor.vue";

export default Vue.extend({
  components: { EchoIcon, FieldEditor },
  props: {
    song: { type: Object as PropType<Song>, required: true },
    editing: Boolean,
    isCreator: Boolean,
  },
  computed: {
    modifiedFormatted(): string {
      return SongProperty.formatDate(this.song.modified);
    },
    createdFormatted(): string {
      return SongProperty.formatDate(this.song.created);
    },
    editedFormatted(): string {
      return SongProperty.formatDate(this.song.edited!);
    },
  },
  methods: {
    formatEchoNest(n: number): string {
      return (n * 100).toFixed(1).toString() + "%";
    },
  },
});
</script>
