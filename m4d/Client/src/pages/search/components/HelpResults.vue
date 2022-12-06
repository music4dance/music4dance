<template>
  <search-results
    id="help-results"
    :search="search"
    name="help pages"
    :entries="entries"
  >
    <p>
      Results from
      <a href="https://music4dance.blog/music4dance-help/">music4dance</a>
      manual
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
      `https://public-api.wordpress.com/wp/v2/sites/music4dance.blog/pages?search=${this.search}&exclude=7683`
    );
    this.entries = results.data.map((w: WordPressEntry) =>
      searchPageFromWordPress(w)
    );
  },
});
</script>
