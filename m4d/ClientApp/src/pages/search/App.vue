<script setup lang="ts">
declare const model_: string;
const search = model_;
</script>

<template>
  <PageFrame id="app">
    <h1>Search Results: {{ search }}</h1>
    <SongResults :search="search" />
    <PageResults :search="search" />
    <PostResults :search="search" />
    <HelpResults :search="search" />
  </PageFrame>
</template>
