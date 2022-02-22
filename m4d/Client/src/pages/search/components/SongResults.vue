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
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { SongFilter } from "@/model/SongFilter";
import { SongHistory } from "@/model/SongHistory";
import axios from "axios";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { Component, Prop, Vue } from "vue-property-decorator";
import ContinueOptions from "./ContinueOptions.vue";
import SearchNav from "./SearchNav.vue";
import ShowMore from "./ShowMore.vue";

@Component({
  components: {
    ContinueOptions,
    Loader,
    SearchNav,
    ShowMore,
    SongTable,
  },
})
export default class SongResults extends Vue {
  @Prop() private search!: string;
  @Prop() environment!: DanceEnvironment;
  private loaded = false;
  private histories: SongHistory[] = [];
  private extraVisible = false;

  public async mounted(): Promise<void> {
    // eslint-disable-next-line @typescript-eslint/no-this-alias
    const filter = this.filter;
    if (!filter) {
      return;
    }
    try {
      const results = await axios.get(
        `/api/song/?filter=${filter.encodedQuery}`
      );
      this.histories = TypedJSON.parseAsArray(results.data, SongHistory);
    } catch (e) {
      console.log(e);
    }
    this.loaded = true;
  }

  private get filter(): SongFilter | null {
    const environment = this.environment;
    if (!environment || !environment.dances) {
      return null;
    }
    const filter = new SongFilter();
    const search = this.search.trim();

    const dance = environment.fromName(search);
    if (dance) {
      filter.dances = dance.id;
      filter.sortOrder = "Dances";
    } else {
      filter.searchString = search;
      filter.sortOrder = "";
    }
    return filter;
  }

  private get visibleHistories(): SongHistory[] {
    const histories = this.histories;
    const ret = this.extraVisible ? histories : histories.slice(0, 4);
    console.log(`extravisible=${this.extraVisible}; length=${ret.length}`);
    return ret;
  }

  private get hasExtra(): boolean {
    return this.histories.length > 4;
  }
}
</script>
