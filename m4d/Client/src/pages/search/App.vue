<template>
  <page
    id="app"
    :consumesEnvironment="true"
    @environment-loaded="onEnvironmentLoaded"
  >
    <h1>Search Results: {{ search }}</h1>
    <song-results
      :search="search"
      :environment="localEnvironment"
      ref="songs"
    ></song-results>
    <page-results id="page-results" :search="search" name="general pages">
      <p>
        Results from the <a href="/">music4dance</a> site <em>except</em> from
        the
        <a href="/song">song library</a>
      </p>
    </page-results>
    <post-results id="blog-results" :search="search" name="blog posts">
      <p>
        Results from the <a href="https://music4dance.blog">music4dance</a> blog
      </p>
    </post-results>
    <help-results id="help-results" :search="search" name="help pages">
      <p>
        Results from
        <a href="https://music4dance.blog/music4dance-help/">music4dance</a>
        manual
      </p>
    </help-results>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import AdminTools from "@/mix-ins/AdminTools";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import "reflect-metadata";
import { Component, Mixins } from "vue-property-decorator";
import HelpResults from "./components/HelpResults.vue";
import PageResults from "./components/PageResults.vue";
import PostResults from "./components/PostResults.vue";
import SongResults from "./components/SongResults.vue";

declare const model: string;

@Component({
  components: { Page, HelpResults, PageResults, PostResults, SongResults },
})
export default class Search extends Mixins(AdminTools, EnvironmentManager) {
  private localEnvironment: DanceEnvironment | null = null;
  private get search(): string {
    return model;
  }

  private async onEnvironmentLoaded(
    environment: DanceEnvironment
  ): Promise<void> {
    // TODO: Figure out why the environment variable on the EnvironmentManager
    //  doesn't appear to be reactive
    this.localEnvironment = environment;
  }
}
</script>
