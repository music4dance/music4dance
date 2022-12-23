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
        :hiddenColumns="['dances', 'length', 'order', 'track']"
      ></song-table>
    </b-col>
  </b-row>
</template>

<script lang="ts">
import SongTable from "@/components/SongTable.vue";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceStats } from "@/model/DanceStats";
import { SongFilter } from "@/model/SongFilter";
import { SongHistory } from "@/model/SongHistory";
import "reflect-metadata";
import { PropType } from "vue";

export default EnvironmentManager.extend({
  components: { SongTable },
  props: {
    histories: { type: Array as PropType<SongHistory[]>, required: true },
    filter: Object as PropType<SongFilter>,
  },
  computed: {
    danceLink(): string {
      return `/dances/${this.danceName}`;
    },

    danceName(): string {
      const dance = this.dance;
      return dance ? dance.name : "";
    },

    dance(): DanceStats | undefined {
      return this.environment.fromId(this.filter.dances!);
    },
  },
});
</script>
