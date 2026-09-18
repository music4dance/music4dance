<script setup lang="ts">
import { getAxiosXsrf } from "@/helpers/GetMenuContext";
import { ref, watch } from "vue";

interface ArtistSuggestion {
  name: string;
  songs: number;
}

interface ArtistSuggestions {
  query: string;
  artists: ArtistSuggestion[];
}

const props = defineProps<{
  id: string;
  /** Match the page's song-count filter so suggestions can't offer artists the search would hide */
  includeAll: boolean;
}>();

const model = defineModel<string>({ required: true });

const emit = defineEmits<{
  search: [];
}>();

const suggestions = ref<ArtistSuggestion[]>([]);
const listId = `${props.id}-suggestions`;

// Responses can land out of order; only the newest request may write.
let latest = 0;
let pending: ReturnType<typeof setTimeout> | undefined;

// Debounced here rather than on the input: BFormInput's own debounce would hold the model back,
// so submitting straight after typing would search the previous value.
const scheduleFetch = (value: string) => {
  clearTimeout(pending);
  pending = setTimeout(() => fetchSuggestions(value), 200);
};

const fetchSuggestions = (value: string) => {
  const query = value.trim();
  const request = ++latest;

  if (query.length < 2) {
    suggestions.value = [];
    return;
  }

  getAxiosXsrf()
    .get("/api/suggestion/artist", { params: { q: query, all: props.includeAll } })
    .then((response) => {
      if (request === latest) {
        suggestions.value = (response.data as ArtistSuggestions).artists ?? [];
      }
    })
    .catch(() => {
      if (request === latest) {
        suggestions.value = [];
      }
    });
};

watch(model, (value) => scheduleFetch(value ?? ""));
</script>

<template>
  <BFormInput
    :id="id"
    v-model="model"
    type="search"
    :list="listId"
    autocomplete="off"
    placeholder="Find an artist"
    aria-label="Find an artist"
    @keyup.enter="emit('search')"
  />
  <datalist :id="listId">
    <option v-for="artist in suggestions" :key="artist.name" :value="artist.name">
      {{ artist.songs }} {{ artist.songs === 1 ? "song" : "songs" }}
    </option>
  </datalist>
</template>
