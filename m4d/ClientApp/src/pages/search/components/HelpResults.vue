<script setup lang="ts">
import type { SearchPage } from "@/models/SearchPage";
import { searchPageFromWordPress, type WordPressEntry } from "@/models/WordPressEntry";
import axios from "axios";
import SearchResults from "./SearchResults.vue";
import { onMounted, ref } from "vue";

const props = defineProps<{
  search: string;
}>();

const entries = ref<SearchPage[] | null>(null);

onMounted(async () => {
  const results = await axios.get(
    `https://public-api.wordpress.com/wp/v2/sites/music4dance.blog/pages?search=${props.search}&exclude=7683`,
  );
  entries.value = results.data.map((w: WordPressEntry) => searchPageFromWordPress(w));
});
</script>

<template>
  <SearchResults id="help-results" :search="search" name="help pages" :entries="entries">
    <p>
      Results from
      <a href="https://music4dance.blog/music4dance-help/">music4dance</a>
      manual
    </p>
  </SearchResults>
</template>
