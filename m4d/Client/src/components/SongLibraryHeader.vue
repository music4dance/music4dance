<template>
  <div>
    <h1 style="text-align: center">Song Library</h1>
    <b-input-group>
      <b-input-group-prepend>
        <b-button v-b-modal.danceChooser variant="outline-primary">{{
          danceLabel
        }}</b-button>
      </b-input-group-prepend>

      <label class="sr-only" for="keywords"
        >Title, Artist, Album, Key Words</label
      >
      <b-form-input
        type="text"
        v-model="filter.searchString"
        placeholder="Title, Artist, Album, Key Words"
        list="auto-complete"
        autofocus
        debounce="100"
        @keyup.enter.native="search"
        @input="checkServiceAndWarn"
      ></b-form-input>
      <datalist id="auto-complete">
        <option v-for="suggestion in suggestions" :key="suggestion">
          {{ suggestion }}
        </option>
      </datalist>

      <b-input-group-append>
        <b-button variant="outline-primary" @click="search">
          <b-icon-search></b-icon-search>
        </b-button>
      </b-input-group-append>
    </b-input-group>
    <b-row>
      <b-col><a :href="searches">Saved Searches</a></b-col>
      <b-col v-if="singleDance" style="text-align: center"
        ><a :href="danceReference">{{ singleDance }} Information</a></b-col
      >
      <b-col style="text-align: right">
        <a :href="advancedSearch">Advanced Search</a>
      </b-col>
    </b-row>
    <dance-chooser
      @chooseDance="chooseDance"
      :danceId="filter.dances"
    ></dance-chooser>
  </div>
</template>

<script lang="ts">
import { wordsToKebab } from "@/helpers/StringHelpers";
import DropTarget from "@/mix-ins/DropTarget";
import { SongFilter } from "@/model/SongFilter";
import axios from "axios";
import { Component, Mixins, Prop, Watch } from "vue-property-decorator";
import DanceChooser from "./DanceChooser.vue";

interface Suggestion {
  value: string;
  data: string;
}

interface SuggestionList {
  query: string;
  suggestions: Suggestion[];
}

@Component({
  components: {
    DanceChooser,
  },
})
export default class SongLibrary extends Mixins(DropTarget) {
  @Prop() private readonly filter!: SongFilter;
  @Prop() private readonly user?: string;

  private suggestions: string[] = [];

  private get danceLabel(): string {
    return !this.filter.dances
      ? "All Dances"
      : this.filter.danceQuery.shortDescription;
  }

  private get advancedSearch(): string {
    return `/song/advancedsearchform?filter=${this.filter.encodedQuery}`;
  }

  private get searches(): string {
    return this.user
      ? "/searches"
      : `/identity/account/login?returnUrl=${this.redirect}`;
  }

  private get singleDance(): string | undefined {
    const danceQuery = this.filter.danceQuery;
    return danceQuery.singleDance ? danceQuery.danceNames[0] : undefined;
  }

  private get danceReference(): string | undefined {
    const danceName = this.singleDance;
    return danceName ? `/dances/${wordsToKebab(danceName)}` : undefined;
  }

  private get redirect(): string {
    const location = window.location;
    return `${location.pathname}${location.search}${location.hash}`;
  }

  private search(): void {
    window.location.href = `/song/filterSearch?filter=${this.filter.encodedQuery}`;
  }

  private chooseSuggestion(): void {
    console.log(this.filter.searchString);
  }

  @Watch("filter.searchString") private autoComplete(): void {
    if (
      !this.filter ||
      !this.filter.searchString ||
      this.filter.searchString.length < 2
    ) {
      return;
    }
    axios
      .get(`/api/suggestion/${this.filter.searchString}`)
      .then((response) => {
        const suggestions = response.data as SuggestionList;
        this.suggestions = suggestions.suggestions.map((s) => s.value);
      })
      .catch((error) => {
        console.log(error);
      });
  }

  private chooseDance(danceId?: string): void {
    const filter = this.filter.clone();
    filter.dances = danceId;
    filter.sortOrder = "Dances";
    filter.page = undefined;
    window.location.href = `/song/filterSearch?filter=${filter.encodedQuery}`;
  }
}
</script>
