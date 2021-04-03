<template>
  <b-table-simple borderless small>
    <b-tr v-if="song.length">
      <b-th>Length</b-th>
      <b-td>{{ song.length }} Seconds</b-td>
    </b-tr>
    <b-tr v-if="song.tempo">
      <b-th>Tempo</b-th>
      <b-td>{{ song.tempo }} BPM</b-td>
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
  </b-table-simple>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";
import EchoIcon from "@/components/EchoIcon.vue";
import { Song } from "@/model/Song";
import { SongProperty } from "@/model/SongProperty";

@Component({
  components: { EchoIcon },
})
export default class SongStats extends Vue {
  @Prop() private readonly song!: Song;

  private get modifiedFormatted(): string {
    return SongProperty.formatDate(this.song.modified);
  }

  private get createdFormatted(): string {
    return SongProperty.formatDate(this.song.created);
  }

  private formatEchoNest(n: number): string {
    return (n * 100).toFixed(1).toString() + "%";
  }
}
</script>
