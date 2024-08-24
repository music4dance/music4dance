<script setup lang="ts">
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
