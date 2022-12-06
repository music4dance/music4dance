<template>
  <page id="app" title="Tag Cloud">
    <tag-cloud :tags="tags"></tag-cloud>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import TagCloud from "@/components/TagCloud.vue";
import { safeTagDatabase } from "@/helpers/DanceEnvironmentManager";
import { BreadCrumbItem, homeCrumb } from "@/model/BreadCrumbItem";
import { Tag } from "@/model/Tag";
import "reflect-metadata";
import Vue from "vue";

export default Vue.extend({
  components: { Page, TagCloud },
  props: {},
  data() {
    return new (class {
      tags: Tag[] = safeTagDatabase().tags;
      breadcrumbs: BreadCrumbItem[] = [
        homeCrumb,
        { text: "Tags", active: true },
      ];
    })();
  },
  computed: {},
});
</script>
