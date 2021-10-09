<template>
  <page
    id="app"
    title="Tag Cloud"
    :consumesEnvironment="true"
    @environment-loaded="onEnvironmentLoaded"
  >
    <tag-cloud :tags="tags"></tag-cloud>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import TagCloud from "@/components/TagCloud.vue";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { Tag } from "@/model/Tag";
import "reflect-metadata";
import { Component, Vue } from "vue-property-decorator";

@Component({
  components: {
    Page,
    TagCloud,
  },
})
export default class App extends Vue {
  private tags: Tag[] = [];

  private onEnvironmentLoaded(environment: DanceEnvironment): void {
    this.tags = environment.tagDatabase.tags;
  }
}
</script>
