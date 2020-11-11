<template>
  <b-row id="top-ten">
    <b-col>
      <hr />
      <h2>
        Top 10 songs for dancing <a :href="filter.url">{{ danceName }}</a>
      </h2>
      <song-table
        :songs="songs"
        :filter="filter"
        :userName="userName"
        :hideSort="true"
        :hiddenColumns="['dances', 'track', 'order']"
      ></song-table>
    </b-col>
  </b-row>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Vue, Prop } from "vue-property-decorator";
import SongTable from "@/components/SongTable.vue";
import Page from "@/components/Page.vue";
import { TypedJSON } from "typedjson";
import { Song } from "@/model/Song";
import { DanceModel } from "@/model/DanceModel";
import { SongFilter } from "@/model/SongFilter";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { DanceStats } from "@/model/DanceStats";

declare const environment: DanceEnvironment;

@Component({
  components: {
    SongTable,
  },
})
export default class TopTen extends Vue {
  @Prop() private readonly songs!: Song[];
  @Prop() private readonly filter!: SongFilter;
  @Prop() private readonly userName!: string;

  private get danceLink(): string {
    return `/dances/${this.danceName}`;
  }

  private get danceName(): string {
    const dance = this.dance;
    return dance ? dance.danceName : "";
  }

  private get dance(): DanceStats | undefined {
    return environment?.fromId(this.filter.dances!);
  }
}
</script>
