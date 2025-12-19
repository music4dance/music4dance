<script setup lang="ts">
import { SongDetailsModel } from "@/models/SongDetailsModel";
import { TypedJSON } from "typedjson";
import { getMenuContext } from "@/helpers/GetMenuContext";

declare const model_: string;
const model = TypedJSON.parse(model_, SongDetailsModel)!;
const context = getMenuContext();
const searchAvailable = context.searchHealthy !== false;

// TODO:
//  Figure out how to be better about updating global taglist - this works, but we're doing
//   it every 6 hours which seems too inferquent - but it's an expensive operation, so
//   don't necessarily want to so it if not needed
//  Consider removing negating properties (this would help state changes)
//  Moderator remove dance: Uses UserProxy to removeTag for the dance
//   for each user, then reproxies to the moderator to negate the dance rating.
//   Consider just having a moderator event in the details that negates cancels out a dance.
</script>

<template>
  <PageFrame id="app">
    <SongCore v-if="searchAvailable" :model="model" />
  </PageFrame>
</template>
