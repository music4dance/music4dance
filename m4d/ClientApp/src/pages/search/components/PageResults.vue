<script setup lang="ts">
import { type SearchPage } from "@/models/SearchPage";
import axios from "axios";
import { onMounted, ref } from "vue";

const props = defineProps<{
  search: string;
}>();

const entries = ref<SearchPage[] | null>(null);

const cleanItem = (item: SearchPage): SearchPage => {
  return {
    url: `${window.location.origin}${item.url}`,
    title: item.title.replace("music4dance catalog: ", ""),
    description: item.description,
  };
};

onMounted(async () => {
  const results = await axios.get(`/api/search?search=${props.search}`);
  entries.value = results.data.map((p: SearchPage) => cleanItem(p));
});
</script>

<template>
  <SearchResults id="page-results" :search="search" name="general pages" :entries="entries">
    <p>
      Results from the <a href="/">music4dance</a> site <em>except</em> from the
      <a href="/song">song library</a>
    </p>
  </SearchResults>
</template>
