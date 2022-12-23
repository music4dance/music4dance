<template>
  <div id="song-results">
    <b-row>
      <b-col md="6" class="flex-grow"
        ><search-nav active="song-results"></search-nav
      ></b-col>
      <b-col md="6"
        ><continue-options :filter="filter"></continue-options
      ></b-col>
    </b-row>
    <loader :loaded="loaded" placeholder="Searching for songs...">
      <div v-if="histories.length > 0">
        <p>
          Results from the
          <a href="/song">music4dance song library</a>
        </p>
        <song-table
          :histories="visibleHistories"
          :filter="filter"
          :hideSort="true"
          :hiddenColumns="['length', 'track']"
        ></song-table>
        <show-more id="extra-songs" v-model="extraVisible"></show-more>
        <continue-options :filter="filter"></continue-options>
      </div>
      <div v-else>
        "{{ search }}" not found in the <a href="/song">music library</a>
      </div>
    </loader>
  </div>
</template>

<script lang="ts">
import Loader from "@/components/Loader.vue";
import SongTable from "@/components/SongTable.vue";
import AdminTools from "@/mix-ins/AdminTools";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import { SongFilter } from "@/model/SongFilter";
import { SongHistory } from "@/model/SongHistory";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { PropType } from "vue";
import ContinueOptions from "./ContinueOptions.vue";
import SearchNav from "./SearchNav.vue";
import ShowMore from "./ShowMore.vue";

export default AdminTools.extend({
  components: { ContinueOptions, Loader, SearchNav, ShowMore, SongTable },
  props: {
    search: { type: String, required: true },
    environment: { type: Object as PropType<DanceEnvironment>, required: true },
  },
  data() {
    return new (class {
      loaded = false;
      histories: SongHistory[] = [];
      extraVisible = false;
    })();
  },
  computed: {
    filter(): SongFilter | null {
      const environment = this.environment;
      if (!environment || !environment.dances) {
        return null;
      }
      const filter = new SongFilter();
      const search = this.search.trim();

      const dance = environment.fromSynonym(search);
      if (dance) {
        filter.dances = dance.id;
        filter.sortOrder = "Dances";
      } else {
        filter.searchString = search;
        filter.sortOrder = "";
      }
      return filter;
    },
    visibleHistories(): SongHistory[] {
      const histories = this.histories;
      const ret = this.extraVisible ? histories : histories.slice(0, 4);
      return ret;
    },
    hasExtra(): boolean {
      return this.histories.length > 4;
    },
  },
  methods: {},
  async mounted(): Promise<void> {
    // eslint-disable-next-line @typescript-eslint/no-this-alias
    const filter = this.filter;
    if (!filter) {
      return;
    }
    try {
      const results = await this.axiosXsrf.get(
        `/api/song/?filter=${filter.encodedQuery}`
      );
      this.histories = TypedJSON.parseAsArray(results.data, SongHistory);
    } catch (e) {
      // eslint-disable-next-line no-console
      console.log(e);
    }
    this.loaded = true;
  },
});
</script>
