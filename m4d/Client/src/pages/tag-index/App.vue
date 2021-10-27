<template>
  <page
    id="app"
    title="Tag Cloud"
    :consumesTags="true"
    @tag-database-loaded="onTagDatabaseLoaded"
  >
    <tag-cloud :tags="tags"></tag-cloud>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import TagCloud from "@/components/TagCloud.vue";
import { Tag } from "@/model/Tag";
import { TagDatabase } from "@/model/TagDatabase";
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

  private onTagDatabaseLoaded(tagDatabase: TagDatabase): void {
    this.tags = tagDatabase.tags;
  }
}
</script>
