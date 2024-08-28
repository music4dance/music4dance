<script setup lang="ts">
import { wordsToKebab } from "@/helpers/StringHelpers";
import { SongFilter } from "@/models/SongFilter";
import { ref } from "vue";

const props = defineProps<{
  filter: SongFilter;
  user?: string;
}>();

const filter = props.filter;
const searchString = ref<string>(filter.searchString ?? "");

const danceLabel = !filter.dances ? "All Dances" : filter.danceQuery.shortDescription;
const advancedSearch = `/song/advancedsearchform?filter=${filter.encodedQuery}`;
const location = window.location;
const redirect = `${location.pathname}${location.search}${location.hash}`;
const searches = props.user ? "/searches" : `/identity/account/login?returnUrl=${redirect}`;
const singleDance = filter.danceQuery.singleDance ? filter.danceQuery.danceNames[0] : undefined;
const danceReference = singleDance ? `/dances/${wordsToKebab(singleDance)}` : undefined;

const search = (): void => {
  const filter = props.filter.clone();
  filter.searchString = searchString.value;
  filter.page = undefined;
  window.location.href = `/song/filterSearch?filter=${filter.encodedQuery}`;
};

const chooseDance = (danceId?: string): void => {
  const filter = props.filter.clone();
  filter.dances = danceId;
  filter.sortOrder = "Dances";
  filter.page = undefined;
  filter.searchString = searchString.value;
  window.location.href = `/song/filterSearch?filter=${filter.encodedQuery}`;
};
</script>

<template>
  <div>
    <h1 style="text-align: center">Song Library</h1>
    <BInputGroup>
      <BButton v-b-modal.dance-chooser variant="outline-primary">{{ danceLabel }}</BButton>
      <SuggestionEntry
        id="keywords"
        v-model="searchString"
        label="Title, Artist, Album, Key Words"
        placeholder="Enter part of a title, artist, album, etc."
        :autofocus="true"
        @search="search"
      />
    </BInputGroup>
    <BRow>
      <BCol><a :href="searches">Saved Searches</a></BCol>
      <BCol v-if="singleDance" style="text-align: center"
        ><a :href="danceReference">{{ singleDance }} Information</a></BCol
      >
      <BCol style="text-align: right">
        <a :href="advancedSearch">Advanced Search</a>
      </BCol>
    </BRow>
    <DanceChooser
      :dance-id="filter.dances"
      :include-groups="true"
      :hide-name-link="true"
      @choose-dance="chooseDance"
    />
  </div>
</template>
