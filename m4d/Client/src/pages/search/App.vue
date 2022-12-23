<template>
  <page id="app">
    <h1>Search Results: {{ search }}</h1>
    <song-results
      :search="search"
      :environment="localEnvironment"
      ref="songs"
    ></song-results>
    <page-results :search="search"></page-results>
    <post-results :search="search"></post-results>
    <help-results :search="search"></help-results>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import { safeEnvironment } from "@/helpers/DanceEnvironmentManager";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import "reflect-metadata";
import Vue from "vue";
import HelpResults from "./components/HelpResults.vue";
import PageResults from "./components/PageResults.vue";
import PostResults from "./components/PostResults.vue";
import SongResults from "./components/SongResults.vue";

declare const model: string;

export default Vue.extend({
  components: { Page, HelpResults, PageResults, PostResults, SongResults },
  data() {
    return new (class {
      localEnvironment: DanceEnvironment | null = null;
    })();
  },
  computed: {
    search(): string {
      return model;
    },
  },
  created(): void {
    this.localEnvironment = safeEnvironment();
  },
});
</script>
