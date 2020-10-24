<template>
  <page id="app" title="Tag Cloud">
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

declare const model: string;

@Component({
  components: {
    Page,
    TagCloud,
  },
})
export default class App extends Vue {
  private tags: Tag[];

  constructor() {
    super();

    this.tags = TypedJSON.parseAsArray(model, Tag).filter((t) => t.icon);
  }
}
</script>
