<script setup lang="ts">
import PageFrame from "@/components/PageFrame.vue";
import SongTable from "@/components/SongTable.vue";
import AdminFooter from "@/components/AdminFooter.vue";
import { AlbumModel } from "@/models/AlbumModel";
import { TypedJSON } from "typedjson";
import { useSongSelector } from "@/composables/useSongSelector";
import { computed } from "vue";

declare const model_: string;

const model = TypedJSON.parse(model_, AlbumModel)!;

const { songs: selected, select: selectSong } = useSongSelector();

const artistRef = computed(() => `/song/artist?name=${encodeURIComponent(model.artist || "")}`);
const hidden = computed(() => (model.artist ? ["Artist"] : []));
</script>

<template>
  <PageFrame id="app">
    <h1>
      Album: {{ model.title }}
      <span v-if="model.artist"
        >by <a :href="artistRef">{{ model.artist }}</a></span
      >
    </h1>
    <SongTable
      :histories="model.histories!"
      :filter="model.filter"
      :hide-sort="true"
      :hidden-columns="hidden"
      @song-selected="selectSong"
    ></SongTable>
    <AdminFooter :model="model" :selected="selected"></AdminFooter>
  </PageFrame>
</template>
