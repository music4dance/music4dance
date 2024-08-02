<script setup lang="ts">
import SpinLoader from "@/components/SpinLoader.vue";
import { type SearchPage } from "@/models/SearchPage";
import ResultItem from "./ResultItem.vue";
import SearchNav from "./SearchNav.vue";
import ShowMore from "./ShowMore.vue";
import { computed, ref } from "vue";

const props = defineProps<{
  id: string;
  search: string;
  name: string;
  entries: SearchPage[] | null;
}>();

const extraVisible = ref(false);

const safeEntries = computed(() => {
  const entries = props.entries;
  return entries ? entries : [];
});

const loaded = computed(() => {
  return !!props.entries;
});

const placeholder = computed(() => {
  return `Search in ${props.name}...`;
});

const emptyText = computed(() => {
  return `"${props.search}" not found in ${props.name}.`;
});

const initialEntries = computed(() => {
  return safeEntries.value.slice(0, 3);
});

const extraEntries = computed(() => {
  return safeEntries.value.slice(3);
});

const extraId = computed(() => {
  return `extra-${props.id}`;
});

const hasExtra = computed(() => {
  return safeEntries.value.length > 3;
});
</script>

<template>
  <div :id="id">
    <hr />
    <SearchNav :active="id"></SearchNav>
    <div class="my-3"></div>
    <SpinLoader :loaded="loaded" :placeholder="placeholder">
      <slot></slot>
      <ResultItem v-for="entry in initialEntries" :key="entry.url" :entry="entry"></ResultItem>
      <div v-if="hasExtra">
        <BCollapse :id="extraId" v-model="extraVisible">
          <ResultItem v-for="entry in extraEntries" :key="entry.url" :entry="entry"></ResultItem>
        </BCollapse>
        <ShowMore v-model="extraVisible" :extra-id="extraId"></ShowMore>
      </div>
      <div v-if="safeEntries.length === 0">
        {{ emptyText }}
      </div>
    </SpinLoader>
  </div>
</template>
