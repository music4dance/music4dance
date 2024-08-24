<script setup lang="ts">
import { SongFilter } from "@/models/SongFilter";
import { SongHistory } from "@/models/SongHistory";
import { TypedJSON } from "typedjson";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { computed, onMounted, ref } from "vue";

const danceDB = safeDanceDatabase();
const props = defineProps<{
  search: string;
}>();
const context = getMenuContext();

const loaded = ref(false);
const histories = ref<SongHistory[]>([]);
const extraVisible = ref(false);

const filter = computed(() => {
  const filter = new SongFilter();
  const search = props.search.trim();

  const dance = danceDB.fromSynonym(search);
  if (dance) {
    filter.dances = dance.id;
    filter.sortOrder = "Dances";
  } else {
    filter.searchString = search;
    filter.sortOrder = "";
  }
  return filter;
});

const visibleHistories = computed(() =>
  extraVisible.value ? histories.value : histories.value.slice(0, 4),
);

onMounted(async () => {
  if (!filter.value) {
    return;
  }
  try {
    const results = await context.axiosXsrf.get(`/api/song/?filter=${filter.value.encodedQuery}`);
    histories.value = TypedJSON.parseAsArray(results.data, SongHistory);
  } catch (e) {
    console.log(e);
  }
  loaded.value = true;
});
</script>

<template>
  <div id="song-results">
    <BRow>
      <BCol md="6" class="flex-grow"><SearchNav active="song-results" /></BCol>
      <BCol md="6"><ContinueOptions :filter="filter" /></BCol>
    </BRow>
    <PageLoader :loaded="loaded" placeholder="Searching for songs...">
      <div v-if="histories.length > 0">
        <p>
          Results from the
          <a href="/song">music4dance song library</a>
        </p>
        <SongTable
          :histories="visibleHistories as SongHistory[]"
          :filter="filter"
          :hide-sort="true"
          :hidden-columns="['length', 'track']"
        />
        <ShowMore v-model="extraVisible" extra-id="extra-songs" />
        <ContinueOptions :filter="filter" />
      </div>
      <div v-else>"{{ search }}" not found in the <a href="/song">music library</a></div>
    </PageLoader>
  </div>
</template>
