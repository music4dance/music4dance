<script lang="ts">
import { SearchPage } from "@/model/SearchPage";
import {
  searchPageFromWordPress,
  WordPressEntry,
} from "@/model/WordPressEntry";
import axios from "axios";
import "reflect-metadata";
import { Component } from "vue-property-decorator";
import SearchResults from "./SearchResults.vue";

@Component
export default class PostResults extends SearchResults {
  protected async getEntries(): Promise<SearchPage[]> {
    const results = await axios.get(
      `https://public-api.wordpress.com/wp/v2/sites/music4dance.blog/posts?search=${this.search}`
    );
    return results.data.map((w: WordPressEntry) => searchPageFromWordPress(w));
  }
}
</script>
