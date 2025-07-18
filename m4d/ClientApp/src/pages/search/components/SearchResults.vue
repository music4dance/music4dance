<script setup lang="ts">
import { type SearchPage } from "@/models/SearchPage";
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
    <SearchNav :active="id" />
    <div class="my-3" />
    <SpinLoader :loaded="loaded" :placeholder="placeholder">
      <slot />
      <ResultItem v-for="entry in initialEntries" :key="entry.url" :entry="entry" />
      <div v-if="hasExtra">
        <BCollapse :id="extraId" v-model="extraVisible">
          <ResultItem v-for="entry in extraEntries" :key="entry.url" :entry="entry" />
        </BCollapse>
        <ShowMore v-model="extraVisible" :extra-id="extraId" />
      </div>
      <div v-if="safeEntries.length === 0">
        {{ emptyText }}
      </div>
    </SpinLoader>
  </div>
</template>
