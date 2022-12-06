<template>
  <search-results
    id="page-results"
    :search="search"
    name="general pages"
    :entries="entries"
  >
    <p>
      Results from the <a href="/">music4dance</a> site <em>except</em> from the
      <a href="/song">song library</a>
    </p>
  </search-results>
</template>
<script lang="ts">
import { SearchPage } from "@/model/SearchPage";
import axios from "axios";
import "reflect-metadata";
import Vue from "vue";
import SearchResults from "./SearchResults.vue";

export default Vue.extend({
  components: { SearchResults },
  props: {
    search: { type: String, required: true },
  },
  data() {
    return new (class {
      entries: SearchPage[] | null = null;
    })();
  },
  methods: {
    cleanItem(item: SearchPage): SearchPage {
      return {
        url: `${window.location.origin}${item.url}`,
        title: item.title.replace("music4dance catalog: ", ""),
        description: item.description,
      };
    },
  },
  async mounted(): Promise<void> {
    const results = await axios.get(`/api/search?search=${this.search}`);
    this.entries = results.data.map((p: SearchPage) => this.cleanItem(p));
  },
});
</script>
