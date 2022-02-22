<script lang="ts">
import { SearchPage } from "@/model/SearchPage";
import axios from "axios";
import "reflect-metadata";
import { Component } from "vue-property-decorator";
import SearchResults from "./SearchResults.vue";

@Component
export default class PageResults extends SearchResults {
  protected async getEntries(): Promise<SearchPage[]> {
    const results = await axios.get(`/api/search?search=${this.search}`);
    return results.data.map((p: SearchPage) => this.cleanItem(p));
  }

  private cleanItem(item: SearchPage): SearchPage {
    return {
      url: `${window.location.origin}${item.url}`,
      title: item.title.replace("music4dance catalog: ", ""),
      description: item.description,
    };
  }
}
</script>
