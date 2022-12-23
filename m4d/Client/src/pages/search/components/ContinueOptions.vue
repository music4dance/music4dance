<template>
  <div class="text-right">
    <b-button variant="outline-primary" :href="songLibrary" class="my-2">
      Continue in Song Library....</b-button
    >
    <b-button
      variant="outline-primary"
      :href="advancedSearch"
      class="ml-2 my-1"
    >
      Advanced Search</b-button
    >
  </div>
</template>

<script lang="ts">
import { SongFilter } from "@/model/SongFilter";
import "reflect-metadata";
import Vue, { PropType } from "vue";

export default Vue.extend({
  props: { filter: { type: Object as PropType<SongFilter>, required: true } },
  data() {
    return new (class {})();
  },
  computed: {
    songLibrary(): string {
      return `/song/filtersearch?filter=${this.filter.encodedQuery}`;
    },

    advancedSearch(): string {
      return `/song/advancedsearchform?filter=${this.filter.encodedQuery}`;
    },
  },
});
</script>
