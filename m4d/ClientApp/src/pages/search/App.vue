<script setup lang="ts">
import PageFrame from "@/components/PageFrame.vue";
import HelpResults from "./components/HelpResults.vue";
import PageResults from "./components/PageResults.vue";
import PostResults from "./components/PostResults.vue";
import SongResults from "./components/SongResults.vue";

declare const model_: string;
const search = model_;
</script>

<template>
  <PageFrame id="app">
    <h1>Search Results: {{ search }}</h1>
    <SongResults :search="search"></SongResults>
    <PageResults :search="search"></PageResults>
    <PostResults :search="search"></PostResults>
    <HelpResults :search="search"></HelpResults>
  </PageFrame>
</template>
