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
import "reflect-metadata";
import { Component, Vue } from "vue-property-decorator";
import TagCloud from "@/components/TagCloud.vue";
import Page from "@/components/Page.vue";
import { TypedJSON } from "typedjson";
import { Tag } from "@/model/Tag";
import { DanceEnvironment } from "@/model/DanceEnvironmet";

@Component({
  components: {
    Page,
    TagCloud,
  },
})
export default class App extends Vue {
  private tags: Tag[] = [];

  private onEnvironmentLoaded(environment: DanceEnvironment): void {
    const tags = environment.tags;
    if (tags) {
      this.tags = tags;
    }
  }
}
</script>
