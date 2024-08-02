<script setup lang="ts">
import { type SearchPage } from "@/models/SearchPage";
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
    `https://public-api.wordpress.com/wp/v2/sites/music4dance.blog/posts?search=${props.search}`,
  );
  entries.value = results.data.map((w: WordPressEntry) => searchPageFromWordPress(w));
});
</script>

<template>
  <SearchResults id="blog-results" :search="search" name="blog posts" :entries="entries">
    <p>Results from the <a href="https://music4dance.blog">music4dance</a> blog</p>
  </SearchResults>
</template>
