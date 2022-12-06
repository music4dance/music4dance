<template>
  <search-results
    id="blog-results"
    :search="search"
    name="blog posts"
    :entries="entries"
  >
    <p>
      Results from the <a href="https://music4dance.blog">music4dance</a> blog
    </p>
  </search-results>
</template>

<script lang="ts">
import { SearchPage } from "@/model/SearchPage";
import {
  searchPageFromWordPress,
  WordPressEntry,
} from "@/model/WordPressEntry";
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
  async mounted(): Promise<void> {
    const results = await axios.get(
      `https://public-api.wordpress.com/wp/v2/sites/music4dance.blog/posts?search=${this.search}`
    );
    this.entries = results.data.map((w: WordPressEntry) =>
      searchPageFromWordPress(w)
    );
  },
});
</script>
