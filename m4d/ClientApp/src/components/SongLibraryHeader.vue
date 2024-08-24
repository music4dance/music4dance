<script setup lang="ts">
import { wordsToKebab } from "@/helpers/StringHelpers";
import { useDropTarget } from "@/composables/useDropTarget";
import { SongFilter } from "@/models/SongFilter";
import DanceChooser from "@/components/DanceChooser.vue";
import SuggestionList from "@/components/SuggestionList.vue";
import { ref } from "vue";

const props = defineProps<{
  filter: SongFilter;
  user?: string;
}>();

const { checkServiceAndWarn } = useDropTarget();

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

      <label class="visually-hidden" for="keywords">Title, Artist, Album, Key Words</label>
      <BFormInput
        v-model="searchString"
        type="text"
        autocomplete="off"
        placeholder="Title, Artist, Album, Key Words"
        list="auto-complete"
        autofocus
        debounce="100"
        @keyup.enter="search"
        @input="checkServiceAndWarn($event.target.value)"
      />
      <SuggestionList id="auto-complete" :search="searchString" />

      <BButton variant="outline-primary" @click="search">
        <IBiSearch />
      </BButton>
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
