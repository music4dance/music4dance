<script setup lang="ts">
import PageFrame from "@/components/PageFrame.vue";
import { SongDetailsModel } from "@/models/SongDetailsModel";
import { SongFilter } from "@/models/SongFilter";
import { SongHistory } from "@/models/SongHistory";
import { SongMergeModel } from "@/models/SongMergeModel";
import SongCore from "@/pages/song/components/SongCore.vue";
import { TypedJSON } from "typedjson";
import { getMenuContext } from "@/helpers/GetMenuContext";

declare const model_: string;

const context = getMenuContext();

if (!context.userName) {
  throw new Error("Attempting to get user specific song details with an anonymous user.");
}

const model = TypedJSON.parse(model_, SongMergeModel)!;
const songDetails = new SongDetailsModel({
  created: true,
  songHistory: SongHistory.merge(model.songId, model.songs, context.userName),
  filter: new SongFilter(),
  userName: context.userName,
});
</script>

<template>
  <PageFrame id="app">
    <SongCore :model="songDetails" :startEditing="true" :creating="true"></SongCore>
  </PageFrame>
</template>
