<script setup lang="ts">
import axios from "axios";
import { ref, watch } from "vue";

interface Suggestion {
  value: string;
  data: string;
}

interface SuggestionList {
  query: string;
  suggestions: Suggestion[];
}

const props = defineProps<{
  id: string;
  search: string;
}>();

const suggestions = ref<string[]>([]);

watch(
  () => props.search,
  (s: string) => {
    if (!s || s.length < 2) {
      return;
    }
    axios
      .get(`/api/suggestion/${s}`)
      .then((response) => {
        const list = response.data as SuggestionList;
        suggestions.value = list.suggestions.map((s) => s.value);
      })
      .catch((error) => {
        // eslint-disable-next-line no-console
        console.log(error);
      });
  },
);
</script>

<template>
  <datalist :id="id">
    <option v-for="suggestion in suggestions" :key="suggestion">
      {{ suggestion }}
    </option>
  </datalist>
</template>
