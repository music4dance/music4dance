<script setup lang="ts">
import { SongDetailsModel } from "@/models/SongDetailsModel";
import { SongFilter } from "@/models/SongFilter";
import { SongHistory } from "@/models/SongHistory";
import { SongMergeModel } from "@/models/SongMergeModel";
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
    <SongCore :model="songDetails" :start-editing="true" :creating="true" />
  </PageFrame>
</template>
