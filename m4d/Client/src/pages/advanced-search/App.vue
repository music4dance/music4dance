<template>
  <page id="app">
    <h1 class="col-sm-12" style="font-size: 22px; text-align: center">
      Advanced Song Search
    </h1>
    <div style="max-width: 600px; margin-left: auto; margin-right: auto">
      <b-form
        id="advanced-search"
        @submit.stop.prevent="onSubmit"
        @reset="onReset"
        :validated="validated"
        novalidate
      >
        <b-form-group
          id="searchStringGroup"
          label="Keywords:"
          label-for="searchString"
        >
          <b-form-input
            id="searchString"
            v-model="keyWords"
            type="text"
            placeholder="Enter Keywords from title, artist, etc... OR a Spotify or Apple share link"
            ref="keywords"
            @input="checkServiceAndWarn"
          ></b-form-input>
        </b-form-group>

        <b-form-group label="Dances:">
          <div style="border: 1px solid #ced4da; boder-radius: 0.25rem">
            <dance-selector
              :danceList="allDances"
              v-model="dances"
            ></dance-selector>
            <div class="d-flex justify-content-between w-100 mx-1 mb-2">
              <b-form-radio-group
                id="dance-connector"
                v-model="danceConnector"
                buttons
                button-variant="outline-secondary"
                :disabled="dances.length < 2"
                class="mx-3"
                size="sm"
              >
                <b-form-radio value="any">Any</b-form-radio>
                <b-form-radio value="all">All</b-form-radio>
              </b-form-radio-group>
            </div>
          </div>
        </b-form-group>

        <b-form-group label="Include Tags:">
          <tag-category-selector
            id="includeTags"
            :tagList="tags"
            chooseLabel="Choose Tags to Include"
            searchLabel="Search Tags"
            emptyLabel="No more tags to choose"
            v-model="includeTags"
          ></tag-category-selector>
        </b-form-group>

        <b-form-group label="Exclude Tags:">
          <tag-category-selector
            id="excludeTags"
            :tagList="tags"
            chooseLabel="Choose Tags to Exclude"
            searchLabel="Search Tags"
            emptyLabel="No more tags to choose"
            v-model="excludeTags"
          ></tag-category-selector>
        </b-form-group>

        <b-form-group
          id="tempo-range-group"
          label="Tempo range (BPM):"
          label-for="tempo-range"
        >
          <b-form-group id="tempo-range">
            <div class="d-flex">
              <b-form-input
                id="tempo-min"
                v-model="tempoMin"
                type="number"
                number
                min="0"
                max="400"
                style="width: 6rem"
              ></b-form-input>
              <span class="mx-2 pt-2">to</span>
              <b-form-input
                id="tempo-max"
                v-model="tempoMax"
                type="number"
                min="0"
                max="400"
                number
                style="width: 6rem"
              ></b-form-input>
              <div class="invalid-feedback">
                Tempos must be between 0 and 400 BPM
              </div>
            </div>
          </b-form-group>
        </b-form-group>

        <b-form-group
          id="length-range-group"
          label="Length range (seconds):"
          label-for="length-range"
        >
          <b-form-group id="length-range">
            <div class="d-flex">
              <b-form-input
                id="length-min"
                v-model="lengthMin"
                type="number"
                number
                min="0"
                max="600"
                style="width: 6rem"
              ></b-form-input>
              <span class="mx-2 pt-2">to</span>
              <b-form-input
                id="length-max"
                v-model="lengthMax"
                type="number"
                min="0"
                max="600"
                number
                style="width: 6rem"
              ></b-form-input>
              <div class="invalid-feedback">
                Length must be between 0 and 600 seconds
              </div>
            </div>
          </b-form-group>
        </b-form-group>

        <b-form-group id="activity-group" label="By User:" label-for="activity">
          <div class="d-flex">
            <b-form-input
              id="user"
              placeholder="UserName or me"
              v-model="displayUser"
              style="width: 10rem"
              class="mr-2"
            ></b-form-input>
            <b-form-select
              id="activity"
              v-model="computedActivity"
              :options="activities"
            ></b-form-select>
          </div>
        </b-form-group>

        <b-form-group
          id="services-group"
          class="mx-2 mb-2"
          label="Available on:"
          label-for="services"
        >
          <b-form-checkbox-group v-model="services" id="services">
            <b-form-checkbox value="A">Amazon</b-form-checkbox>
            <b-form-checkbox value="I">ITunes</b-form-checkbox>
            <b-form-checkbox value="S">Spotify</b-form-checkbox>
          </b-form-checkbox-group>
        </b-form-group>

        <b-form-group
          id="bonuses-group"
          class="mx-2 mb-2"
          label="Bonus content:"
          label-for="bonuses"
        >
          <b-form-checkbox-group v-model="bonuses" id="bonuses">
            <b-form-checkbox value="P"
              >Not found in any publisher catalog</b-form-checkbox
            >
            <b-form-checkbox value="D"
              >Not categorized by dance</b-form-checkbox
            >
          </b-form-checkbox-group>
          <template v-slot:description>
            <a href="https://music4dance.blog/music4dance-help/subscriptions/"
              >Premium content</a
            >
          </template>
        </b-form-group>

        <b-form-group id="sort-group" label="Sort By:" label-for="sort">
          <b-form-select
            id="sort"
            v-model="sort"
            :options="validSortOptions"
            required
          ></b-form-select>
          <b-form-radio-group
            id="sort-order"
            v-model="order"
            name="sort-order"
            class="mt-2"
          >
            <b-form-radio value="asc"
              >Ascending (A-Z, Slow-Fast, Newest-Oldest,
              Shortest-Longest)</b-form-radio
            >
            <b-form-radio value="desc"
              >Descending (Z-A, Fast-Slow, Oldest-Newest,
              Longest-Shortest)</b-form-radio
            >
          </b-form-radio-group>
        </b-form-group>

        <div class="d-flex justify-content-between w-100 mx-1 mb-2">
          <b-button type="reset" variant="secondary">Reset</b-button>
          <b-button type="search" variant="primary">Submit</b-button>
        </div>
      </b-form>
      <b-card class="mt-3" header="Form Data Result" v-if="showDiagnostics">
        <pre class="m-0">
searchString = {{ keyWords }}
dances = {{ dances }}
danceConnector = {{ danceConnector }}
tempoMin = {{ tempoMin }}
tempoMax = {{ tempoMax }}
lengthMin = {{ lengthMin }}
lengthMax = {{ lengthMax }}
activity = {{ activity }}
services = {{ services }}
sort = {{ sort }}
order = {{ order }}
bonus = {{ bonuses }}
includeTags = {{ includeTags }}
excludeTags = {{ excludeTags }}

filter = {{ songFilter }}
      </pre
        >
      </b-card>
    </div>
  </page>
</template>

<script lang="ts">
import DanceSelector from "@/components/DanceSelector.vue";
import Page from "@/components/Page.vue";
import TagCategorySelector from "@/components/TagCategorySelector.vue";
import {
  safeEnvironment,
  safeTagDatabase,
} from "@/helpers/DanceEnvironmentManager";
import AdminTools from "@/mix-ins/AdminTools";
import DropTarget from "@/mix-ins/DropTarget";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import { DanceQuery } from "@/model/DanceQuery";
import { NamedObject } from "@/model/NamedObject";
import { SongFilter } from "@/model/SongFilter";
import { SongSort, SortOrder } from "@/model/SongSort";
import { Tag } from "@/model/Tag";
import { UserQuery } from "@/model/UserQuery";
import "reflect-metadata";
import Vue from "vue";

interface SortOption {
  text: string;
  value: SortOrder | null;
}

interface Activity {
  text: string;
  value: string;
}

export default AdminTools.extend({
  components: {
    DanceSelector,
    Page,
    TagCategorySelector,
  },
  mixins: [EnvironmentManager, DropTarget],
  data() {
    return new (class {
      danceEnvironment: DanceEnvironment | null = null;
      showDiagnostics = false;
      keyWords = "";

      dances: string[] = [];
      danceConnector = "any";
      tags: Tag[] = [];

      includeTags: string[] = [];
      excludeTags: string[] = [];

      tempoMin = 0;
      tempoMax = 400;

      lengthMin = 0;
      lengthMax = 600;

      user = "";
      displayUser = "";

      bonuses: string[] = [];
      validated = false;
      services: string[] = [];
      activity = "NT";

      sortOptions: SortOption[] = [
        { text: "Title", value: SortOrder.Title },
        { text: "Artist", value: SortOrder.Artist },
        { text: "Tempo", value: SortOrder.Tempo },
        { text: "Length", value: SortOrder.Length },
        { text: "Dance Rating", value: SortOrder.Dances },
        { text: "Last Modified", value: SortOrder.Modified },
        { text: "Last Edited", value: SortOrder.Edited },
        { text: "When Added", value: SortOrder.Created },
        { text: "Energy", value: SortOrder.Energy },
        { text: "Mood", value: SortOrder.Mood },
        { text: "Strength of Beat", value: SortOrder.Beat },
        { text: "Comments", value: SortOrder.Comments },
        { text: "Closest Match", value: null },
      ];
      sort: string | null = "Dances";
      order = "asc";
    })();
  },
  computed: {
    activities(): Activity[] {
      const user = this.displayUser;
      const empty = { text: "Don't filter on user activity", value: "NT" };

      if (!user) {
        return [empty];
      }

      const my = user === "me" ? "my" : user + "'s";
      const i = user === "me" ? "I have" : user + " has";

      var items = [
        { text: `Include all songs in ${my} favorites`, value: "IL" },
        { text: `Exclude all songs in ${my} favorites`, value: "XL" },
        { text: `Include all songs ${i} tagged`, value: "IT" },
        { text: `Exclude all songs ${i} tagged`, value: "XT" },
        { text: `Exclude all songs in ${my} blocked list`, value: "XH" },
        { text: `Include all songs in ${my} blocked list`, value: "IH" },
      ];

      const dances = this.dances;
      if (dances.length > 0) {
        items.unshift({
          text: `Include all songs ${i} voted against ${this.danceNames.join(
            ", "
          )}`,
          value: "IX",
        });
        items.unshift({
          text: `Include all songs ${i} voted for ${this.danceNames.join(
            ", "
          )}`,
          value: "ID",
        });
      }
      items.unshift(empty);
      return items;
    },
    allDances(): NamedObject[] {
      const environment = this.danceEnvironment;
      return environment && environment.tree ? environment.flatDances : [];
    },
    computedActivity: {
      get: function (): string {
        return this.displayUser ? this.activity : "NT";
      },
      set: function (value: string): void {
        if (this.hasUser) {
          this.activity = value;
        }
      },
    },
    danceNames(): string[] {
      const environment = this.danceEnvironment;
      return environment
        ? this.dances.map((d) => environment.fromId(d)!.name)
        : [];
    },
    hasUser(): boolean {
      return !!(this.user || this.displayUser);
    },
    isAnonymous(): boolean {
      return this.displayUser === "Anonymous";
    },
    lengthValid(): boolean {
      return this.lengthMin <= this.lengthMax;
    },
    queryFilter(): SongFilter | undefined {
      const params = new URLSearchParams(window.location.search);
      const filterString = params.get("filter");

      return filterString ? SongFilter.buildFilter(filterString) : undefined;
    },
    songFilter(): SongFilter {
      const danceQuery = DanceQuery.fromParts(
        this.dances,
        this.danceConnector === "all"
      );
      const userQuery = UserQuery.fromParts(
        this.computedActivity ? this.computedActivity : undefined,
        this.isAnonymous ? this.user : this.displayUser
      );
      const filter = new SongFilter();
      let level = 0;
      if (this.bonuses.indexOf("P") !== -1) {
        level = 1;
      }
      if (this.bonuses.indexOf("D") !== -1) {
        level += 2;
      }

      filter.action = "Advanced";
      filter.searchString = this.keyWords;
      filter.dances = danceQuery.query;
      filter.sortOrder = SongSort.fromParts(
        this.sort ?? undefined,
        this.order
      ).query;
      filter.user = userQuery.query;
      filter.purchase = this.services.join("");
      filter.tempoMin = this.tempoMin === 0 ? undefined : this.tempoMin;
      filter.tempoMax = this.tempoMax >= 400 ? undefined : this.tempoMax;
      filter.lengthMin = this.lengthMin === 0 ? undefined : this.lengthMin;
      filter.lengthMax = this.lengthMax >= 400 ? undefined : this.lengthMax;
      filter.tags = this.tagList;
      filter.level = level ? level : undefined;

      return filter;
    },
    sourceFilter(): SongFilter {
      const queryFilter = this.queryFilter;
      return queryFilter ? queryFilter : new SongFilter();
    },
    tagList(): string {
      const lists: string[] = [];
      if (this.includeTags.length > 0) {
        lists.push(this.buildSingleTagList(this.includeTags, "+"));
      }
      if (this.excludeTags.length > 0) {
        lists.push(this.buildSingleTagList(this.excludeTags, "-"));
      }
      return lists.join("|");
    },
    tempoValid(): boolean {
      return this.tempoMin <= this.tempoMax;
    },
    validSortOptions(): SortOption[] {
      const singleDance = this.songFilter.singleDance;
      return this.sortOptions.filter(
        (opt) => opt.value !== SortOrder.Dances || singleDance
      );
    },
  },
  methods: {
    buildSingleTagList(tags: string[], decorator: string) {
      return tags.map((t) => `${decorator}${t}`).join("|");
    },
    extractTags(tags: string, include: boolean): string[] {
      if (!tags) {
        return [];
      }

      const qualifier = include ? "+" : "-";
      const parts = tags.split("|").map((p) => p.trim());
      let filtered = parts
        .filter((p) => p.startsWith(qualifier))
        .map((p) => p.slice(1));
      if (include) {
        filtered = filtered.concat(
          parts.filter((p) => !p.startsWith("+") && !p.startsWith("-"))
        );
      }

      return filtered;
    },
    async onSubmit(): Promise<void> {
      const form = document.getElementById(
        "advanced-search"
      ) as HTMLFormElement;

      if (form.checkValidity() === true) {
        if (this.tempoMin > this.tempoMax) {
          const tempo = this.tempoMax;
          this.tempoMax = this.tempoMin;
          this.tempoMin = tempo;
        }

        if (this.lengthMin > this.lengthMax) {
          const length = this.lengthMax;
          this.lengthMax = this.lengthMin;
          this.lengthMin = length;
        }

        const loc = window.location;
        const query = this.songFilter.encodedQuery;

        const state = window.location.pathname + `?filter=${query}`;
        window.history.replaceState(null, "", state);

        window.location.href = `${loc.origin}/song/filtersearch?filter=${query}`;
      }

      this.validated = true;
    },
    onReset(evt: Event) {
      evt.preventDefault();
      const userName = this.isAnonymous ? this.userName : this.displayUser;

      this.keyWords = "";
      this.dances.splice(0);
      this.danceConnector = "any";
      this.includeTags.splice(0);
      this.excludeTags.splice(0);
      this.tempoMin = 0;
      this.tempoMax = 400;
      this.lengthMin = 0;
      this.lengthMax = 600;
      if (userName) {
        this.user = userName;
        this.activity = "IH";
      } else {
        this.user = "";
        this.activity = "NT";
      }
      this.displayUser = "";
      this.services.splice(0);
      this.sort = null;
      this.order = "asc";
      this.bonuses.splice(0);

      this.validated = false;
    },
    loadTagDatabase(): void {
      const tagDatabase = safeTagDatabase();
      const filter = this.sourceFilter;

      const danceQuery = new DanceQuery(filter.dances);

      this.keyWords = filter.searchString ?? "";

      this.dances = danceQuery.danceList;
      this.danceConnector = danceQuery.isExclusive ? "all" : "any";

      const sort = new SongSort(filter.sortOrder);
      this.sort = sort.order ?? null;
      this.order = sort.direction;

      this.services = filter.purchase ? filter.purchase.trim().split("") : [];
      const userQuery = new UserQuery(filter.user);
      this.activity = userQuery.parts;
      this.user = userQuery.userName ?? this.userName ?? "";
      this.displayUser = userQuery.displayName;
      this.tags = tagDatabase.tags;

      this.tempoMin = filter.tempoMin ?? 0;
      this.tempoMax = filter.tempoMax ?? 400;

      this.lengthMin = filter.lengthMin ?? 0;
      this.lengthMax = filter.lengthMax ?? 600;

      this.includeTags = filter.tags ? this.extractTags(filter.tags, true) : [];
      this.excludeTags = filter.tags
        ? this.extractTags(filter.tags, false)
        : [];

      this.bonuses = [];
      if (filter.level && filter.level & 1) {
        this.bonuses.push("P");
      }
      if (filter.level && filter.level & 2) {
        this.bonuses.push("D");
      }

      this.$nextTick(() =>
        ((this.$refs.keywords as Vue).$el as HTMLElement).focus()
      );
    },
  },
  mounted(): void {
    this.danceEnvironment = safeEnvironment();
    this.loadTagDatabase();
  },
});
</script>
