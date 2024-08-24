<script setup lang="ts">
import { SongFilter } from "@/models/SongFilter";

const props = defineProps<{ filter: SongFilter }>();
const songLibrary = `/song/filtersearch?filter=${props.filter.encodedQuery}`;
const advancedSearch = `/song/advancedsearchform?filter=${props.filter.encodedQuery}`;
</script>

<template>
  <div class="d-grid gap-2 d-md-flex justify-content-md-end">
    <BButton variant="outline-primary" :href="songLibrary" class="my-2">
      Continue in Song Library....</BButton
    >
    <BButton variant="outline-primary" :href="advancedSearch" class="ms-2 my-2">
      Advanced Search</BButton
    >
  </div>
</template>
