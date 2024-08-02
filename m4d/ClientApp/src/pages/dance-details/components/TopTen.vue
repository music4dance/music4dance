<script setup lang="ts">
import SongTable from "@/components/SongTable.vue";
import { SongFilter } from "@/models/SongFilter";
import { SongHistory } from "@/models/SongHistory";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";

const props = defineProps<{ histories: SongHistory[]; filter: SongFilter }>();

const dance = safeDanceDatabase().fromId(props.filter.dances!);
const danceName = dance ? dance.name : "";
</script>

<template>
  <BRow id="top-ten">
    <BCol>
      <hr />
      <h2>
        Top 10 songs for dancing <a :href="filter.url">{{ danceName }}</a>
      </h2>
      <SongTable
        :histories="histories"
        :filter="filter"
        :hide-sort="true"
        :hidden-columns="['dances', 'length', 'order', 'track']"
      ></SongTable>
    </BCol>
  </BRow>
</template>
