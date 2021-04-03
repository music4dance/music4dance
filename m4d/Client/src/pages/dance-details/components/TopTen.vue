<template>
  <b-row id="top-ten">
    <b-col>
      <hr />
      <h2>
        Top 10 songs for dancing <a :href="filter.url">{{ danceName }}</a>
      </h2>
      <song-table
        :histories="histories"
        :filter="filter"
        :hideSort="true"
        :hiddenColumns="['dances', 'track', 'order']"
      ></song-table>
    </b-col>
  </b-row>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";
import SongTable from "@/components/SongTable.vue";
import { SongFilter } from "@/model/SongFilter";
import { SongHistory } from "@/model/SongHistory";
import { DanceStats } from "@/model/DanceStats";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";

@Component({
  components: {
    SongTable,
  },
})
export default class TopTen extends Mixins(EnvironmentManager) {
  @Prop() private readonly histories!: SongHistory[];
  @Prop() private readonly filter!: SongFilter;

  private get danceLink(): string {
    return `/dances/${this.danceName}`;
  }

  private get danceName(): string {
    const dance = this.dance;
    return dance ? dance.danceName : "";
  }

  private get dance(): DanceStats | undefined {
    return this.environment.fromId(this.filter.dances!);
  }
}
</script>
