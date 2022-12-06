<template>
  <div :id="id">
    <hr />
    <search-nav :active="id"></search-nav>
    <div class="my-3"></div>
    <loader :loaded="loaded" :placeholder="placeholder">
      <slot></slot>
      <result-item
        v-for="entry in initialEntries"
        :key="entry.id"
        :entry="entry"
      ></result-item>
      <div v-if="hasExtra">
        <b-collapse :id="extraId" v-model="extraVisible">
          <result-item
            v-for="entry in extraEntries"
            :key="entry.id"
            :entry="entry"
          ></result-item>
        </b-collapse>
        <show-more :id="extraId" v-model="extraVisible"></show-more>
      </div>
      <div v-if="safeEntries.length === 0">
        {{ emptyText }}
      </div>
    </loader>
  </div>
</template>

<script lang="ts">
import Loader from "@/components/Loader.vue";
import { SearchPage } from "@/model/SearchPage";
import "reflect-metadata";
import Vue, { PropType } from "vue";
import ResultItem from "./ResultItem.vue";
import SearchNav from "./SearchNav.vue";
import ShowMore from "./ShowMore.vue";

export default Vue.extend({
  components: { Loader, ResultItem, SearchNav, ShowMore },
  props: {
    id: { type: String, required: true },
    search: { type: String, required: true },
    name: { type: String, required: true },
    entries: { type: [] as PropType<SearchPage[] | null> },
  },
  data() {
    return new (class {
      extraVisible = false;
    })();
  },
  computed: {
    safeEntries(): SearchPage[] {
      const entries = this.entries;
      return entries ? entries : [];
    },
    loaded(): boolean {
      return !!this.entries;
    },
    placeholder(): string {
      return `Search in ${this.name}...`;
    },
    emptyText(): string {
      return `"${this.search}" not found in ${this.name}.`;
    },
    initialEntries(): SearchPage[] {
      return this.safeEntries.slice(0, 3);
    },
    extraEntries(): SearchPage[] {
      return this.safeEntries.slice(3);
    },
    extraId(): string {
      return `extra-${this.id}`;
    },
    hasExtra(): boolean {
      return this.safeEntries.length > 3;
    },
    buttonText(): string {
      return this.extraVisible ? "Show Less" : "Show More";
    },
  },
});
</script>
