<template>
  <li class="list-clean">
    <b-icon :icon="valenceIcon" class="mr-1"></b-icon>
    <b-icon-heart-fill v-if="type === 'l'" variant="danger"></b-icon-heart-fill>
    <b-iconstack v-else-if="type === 'h'">
      <b-icon-heart
        stacked
        variant="secondary"
        scale="0.75"
        shift-v="-1"
      ></b-icon-heart>
      <b-icon-x-circle stacked variant="danger"></b-icon-x-circle>
    </b-iconstack>
    <b-icon-pencil v-else></b-icon-pencil>
    <a :href="url" v-html="formattedText" class="ml-1"></a>
    <span v-if="include"> ({{ count }})</span>
  </li>
</template>

<script lang="ts">
import { SongFilter } from "@/model/SongFilter";
import { SortOrder } from "@/model/SongSort";
import Vue from "vue";

export default Vue.extend({
  props: {
    userName: { type: String, required: true },
    displayName: { type: String, required: true },
    text: { type: String, required: true },
    type: { type: String, required: true },
    include: { type: Boolean, required: true },
    count: { type: Number, required: true },
  },
  computed: {
    url(): string {
      const filter = new SongFilter();
      filter.user = `${this.include ? "+" : "-"}${this.userName}|${this.type}`;
      filter.sortOrder = SortOrder.Modified;
      return `/song/filterSearch?filter=${filter.encodedQuery}`;
    },
    valenceIcon(): string {
      return this.include ? "patch-plus" : "patch-minus";
    },
    formattedText(): string {
      return this.text.replace("{{ userName }}", this.displayName);
    },
  },
});
</script>
