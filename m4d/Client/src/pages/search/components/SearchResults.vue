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
      <div v-if="entries.length === 0">
        {{ emptyText }}
      </div>
    </loader>
  </div>
</template>

<script lang="ts">
import Loader from "@/components/Loader.vue";
import { SearchPage } from "@/model/SearchPage";
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";
import ResultItem from "./ResultItem.vue";
import SearchNav from "./SearchNav.vue";
import ShowMore from "./ShowMore.vue";

@Component({ components: { Loader, ResultItem, SearchNav, ShowMore } })
export default class SearchResults extends Vue {
  @Prop() private id!: string;
  @Prop() private title!: string;
  @Prop() protected search!: string;
  @Prop() private name!: string;

  private loaded = false;
  private extraVisible = false;
  private entries: SearchPage[] = [];

  private async mounted(): Promise<void> {
    try {
      this.entries = await this.getEntries();
      this.loaded = true;
    } catch (e) {
      // eslint-disable-next-line no-console
      console.log(`Failed to search for {title}: ${e}`);
    }
  }

  protected get placeholder(): string {
    return `Search in ${this.name}...`;
  }

  protected get emptyText(): string {
    return `"${this.search}" not found in ${this.name}.`;
  }

  protected async getEntries(): Promise<SearchPage[]> {
    throw new Error("Derived class must implement getEntries");
  }

  private get initialEntries(): SearchPage[] {
    return this.entries.slice(0, 3);
  }

  private get extraEntries(): SearchPage[] {
    return this.entries.slice(3);
  }

  private get extraId(): string {
    return `extra-${this.id}`;
  }

  private get hasExtra(): boolean {
    return this.entries.length > 3;
  }

  private get buttonText(): string {
    return this.extraVisible ? "Show Less" : "Show More";
  }
}
</script>
