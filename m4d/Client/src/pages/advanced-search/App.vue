<template>
  <page
    id="app"
    :consumesEnvironment="true"
    @environment-loaded="onEnvironmentLoaded"
  >
    <h1 class="col-sm-12" style="font-size: 22px; text-align: center">
      Advanced Song Search
    </h1>
    <div style="max-width: 600px; margin-left: auto; margin-right: auto">
      <b-form
        id="advanced-search"
        @submit="onSubmit"
        @reset="onReset"
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
            placeholder="Enter Keywords from title, artist, etc..."
            ref="keywords"
          ></b-form-input>
        </b-form-group>

        <b-form-group label="Dances:">
          <div style="border: 1px solid #ced4da; boder-radius: 0.25rem">
            <dance-selector
              :danceList="model.dances"
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
              <b-form-checkbox
                id="dance-inferred"
                class="mx-3"
                :disabled="dances.length === 0"
                v-model="danceInferred"
              >
                Include Inferred
              </b-form-checkbox>
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
                max="250"
                style="width: 6rem"
              ></b-form-input>
              <span class="mx-2 pt-2">to</span>
              <b-form-input
                id="tempo-max"
                v-model="tempoMax"
                type="number"
                min="0"
                max="250"
                number
                style="width: 6rem"
              ></b-form-input>
              <div class="invalid-feedback">
                Tempos must be between 0 and 250 BPM
              </div>
            </div>
          </b-form-group>
        </b-form-group>

        <b-form-group
          id="activity-group"
          label="My Activity:"
          label-for="activity"
        >
          <b-form-select
            id="activity"
            v-model="activity"
            :options="activities"
          ></b-form-select>
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
            :options="sortOptions"
            required
          ></b-form-select>
          <b-form-radio-group
            id="sort-order"
            v-model="order"
            name="sort-order"
            class="mt-2"
          >
            <b-form-radio value="asc"
              >Ascending (A-Z, Slow-Fast, Newest-Oldest)</b-form-radio
            >
            <b-form-radio value="desc"
              >Descending (Z-A, Fast-Slow, Oldest-Newest)</b-form-radio
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
danceInferred = {{ danceInferred }}
tempoMin = {{ tempoMin }}
tempoMax = {{ tempoMax }}
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
import "reflect-metadata";
import { Component, Vue } from "vue-property-decorator";
import Page from "@/components/Page.vue";
import { SongFilter } from "@/model/SongFilter";
import { DanceQuery } from "@/model/DanceQuery";
import { UserQuery } from "@/model/UserQuery";
import { SongSort, SortOrder } from "@/model/SongSort";
import { SearchModel } from "./searchModel";
import DanceSelector from "@/components/DanceSelector.vue";
import TagCategorySelector from "@/components/TagCategorySelector.vue";
import { TypedJSON } from "typedjson";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { Tag } from "@/model/Tag";

declare const model: SearchModel;

@Component({
  components: {
    DanceSelector,
    Page,
    TagCategorySelector,
  },
})
export default class App extends Vue {
  private showDiagnostics = false;
  private keyWords = "";

  private dances: string[] = [];
  private danceConnector = "any";
  private danceInferred = false;

  private environment: DanceEnvironment | null = null;
  private get tags(): Tag[] {
    const environment = this.environment;
    return environment ? environment.tagDatabase.tags : [];
  }

  private includeTags: string[] = [];
  private excludeTags: string[] = [];

  private tempoMin = 0;
  private tempoMax = 250;

  private activity = "NT";

  private activities = [
    { text: "Don't filter on my activity", value: "NT" },
    { text: "Include all songs I've marked LIKE", value: "IL" },
    { text: "Exclude all songs I've marked LIKE", value: "XL" },
    { text: "Include all songs I've tagged", value: "IT" },
    { text: "Exclude all songs I've tagged", value: "XT" },
    { text: "Include all songs I've marked DON'T LIKE", value: "IH" },
    { text: "Exclude all songs I've marked DON'T LIKE", value: "XH" },
  ];

  private services: string[] = [];

  private sortOptions = [
    { text: "Title", value: SortOrder.Title },
    { text: "Artist", value: SortOrder.Artist },
    { text: "Tempo", value: SortOrder.Tempo },
    { text: "Dance Rating", value: SortOrder.Dances },
    { text: "Last Modified", value: SortOrder.Modified },
    { text: "Last Edited", value: SortOrder.Edited },
    { text: "When Added", value: SortOrder.Created },
    { text: "Energy", value: SortOrder.Energy },
    { text: "Mood", value: SortOrder.Mood },
    { text: "Strength of Beat", value: SortOrder.Beat },
    { text: "Closest Match", value: null },
  ];
  private sort: string | null = "Dances";
  private order = "asc";
  private bonuses: string[] = [];
  private model: SearchModel;

  constructor() {
    super();

    this.model = TypedJSON.parse(model, SearchModel)!;
    let filter = this.getQueryFilter();
    if (!filter) {
      filter = this.model.filter;
    }
    const danceQuery = new DanceQuery(filter.dances);

    this.keyWords = filter.searchString ?? "";

    this.dances = danceQuery.danceList;
    this.danceConnector = danceQuery.isExclusive ? "all" : "any";
    this.danceInferred = danceQuery.includeInferred;

    const sort = new SongSort(filter.sortOrder);
    this.sort = sort.order ?? null;
    this.order = sort.direction;

    this.services = filter.purchase ? filter.purchase.trim().split("") : [];

    this.activity = new UserQuery(filter.user).parts;

    this.tempoMin = filter.tempoMin ?? 0;
    this.tempoMax = filter.tempoMax ?? 250;

    this.includeTags = filter.tags ? this.extractTags(filter.tags, true) : [];
    this.excludeTags = filter.tags ? this.extractTags(filter.tags, false) : [];

    this.bonuses = [];
    if (filter.level && filter.level & 1) {
      this.bonuses.push("P");
    }
    if (filter.level && filter.level & 2) {
      this.bonuses.push("D");
    }
  }

  private get tempoValid(): boolean {
    return this.tempoMin <= this.tempoMax;
  }

  private get songFilter(): SongFilter {
    const danceQuery = DanceQuery.fromParts(
      this.dances,
      this.danceConnector === "all",
      this.danceInferred
    );
    const userQuery = UserQuery.fromParts(
      this.activity ? this.activity : undefined
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
    filter.tempoMax = this.tempoMax >= 250 ? undefined : this.tempoMax;
    filter.tags = this.buildTagList();
    filter.level = level ? level : undefined;

    return filter;
  }

  private getQueryFilter(): SongFilter | undefined {
    const params = new URLSearchParams(window.location.search);
    const filterString = params.get("filter");

    return filterString ? SongFilter.buildFilter(filterString) : undefined;
  }

  private onSubmit(evt: Event) {
    evt.preventDefault();
    evt.stopPropagation(); // Do we need this?

    const form = document.getElementById("advanced-search") as HTMLFormElement;

    if (form.checkValidity() === true) {
      if (this.tempoMin > this.tempoMax) {
        const tempo = this.tempoMax;
        this.tempoMax = this.tempoMin;
        this.tempoMin = tempo;
      }

      const loc = window.location;
      const query = this.songFilter.encodedQuery;

      window.location.href = `${loc.origin}/song/filtersearch?filter=${query}`;
    }

    form.classList.add("was-validated");
  }

  private onReset(evt: Event) {
    evt.preventDefault();

    this.keyWords = "";
    this.dances.splice(0);
    this.danceConnector = "any";
    this.danceInferred = false;
    this.includeTags.splice(0);
    this.excludeTags.splice(0);
    this.tempoMin = 0;
    this.tempoMax = 250;
    this.activity = "NT";
    this.services.splice(0);
    this.sort = "Dances";
    this.order = "asc";
    this.bonuses.splice(0);
  }

  private buildTagList(): string {
    const lists: string[] = [];
    if (this.includeTags.length > 0) {
      lists.push(this.buildSingleTagList(this.includeTags, "+"));
    }
    if (this.excludeTags.length > 0) {
      lists.push(this.buildSingleTagList(this.excludeTags, "-"));
    }
    return lists.join("|");
  }

  private buildSingleTagList(tags: string[], decorator: string) {
    return tags.map((t) => `${decorator}${t}`).join("|");
  }

  private extractTags(tags: string, include: boolean): string[] {
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
  }

  private async onEnvironmentLoaded(
    environment: DanceEnvironment
  ): Promise<void> {
    this.environment = environment;

    await this.$nextTick();
    ((this.$refs.keywords as Vue).$el as HTMLElement).focus();
  }
}
</script>
