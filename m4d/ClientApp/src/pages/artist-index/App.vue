<script setup lang="ts">
import { ArtistIndexModel } from "@/models/ArtistIndexModel";
import { artistPageUrl } from "@/models/ArtistNames";
import { type BreadCrumbItem, homeCrumb, songCrumb } from "@/models/BreadCrumbItem";
import { TypedJSON } from "typedjson";
import { computed, ref } from "vue";

declare const model_: string;

const model = TypedJSON.parse(model_, ArtistIndexModel)!;

const breadcrumbs: BreadCrumbItem[] = [homeCrumb, songCrumb, { text: "Artists", active: true }];

const includeAll = computed(() => model.minSongs <= 1);
const query = ref(model.query ?? "");

const pageUrl = (params: { letter?: string; q?: string; all?: boolean }): string => {
  const search = new URLSearchParams();
  if (params.letter) {
    search.set("letter", params.letter);
  }
  if (params.q) {
    search.set("q", params.q);
  }
  if (params.all) {
    search.set("all", "true");
  }
  const qs = search.toString();
  return qs ? `/song/artists?${qs}` : "/song/artists";
};

const heading = computed(() => {
  if (model.query) {
    return `Artists matching "${model.query}"`;
  }
  if (model.letter) {
    return model.letter === "#" ? "Artists starting with other characters" : `Artists: ${model.letter}`;
  }
  return "Most popular artists";
});

const toggleAllUrl = computed(() =>
  pageUrl({ letter: model.letter, q: model.query, all: !includeAll.value }),
);

const search = () => {
  window.location.href = pageUrl({ q: query.value.trim(), all: includeAll.value });
};
</script>

<template>
  <PageFrame id="app" title="Artists" :breadcrumbs="breadcrumbs">
    <BForm class="mb-3" role="search" @submit.prevent="search">
      <BInputGroup>
        <BFormInput
          v-model="query"
          type="search"
          placeholder="Find an artist"
          aria-label="Find an artist"
        />
        <BButton type="submit" variant="primary"><IBiSearch /> Search</BButton>
      </BInputGroup>
    </BForm>

    <nav aria-label="Artists by letter" class="mb-3">
      <BNav pills small class="flex-wrap">
        <BNavItem
          v-for="bucket in model.buckets"
          :key="bucket.bucket"
          :href="pageUrl({ letter: bucket.bucket, all: includeAll })"
          :active="bucket.bucket === model.letter"
          :disabled="bucket.artists === 0"
          :title="`${bucket.artists} artists`"
          >{{ bucket.bucket }}</BNavItem
        >
      </BNav>
    </nav>

    <h2 class="h4">{{ heading }}</h2>
    <p class="text-body-secondary">
      <template v-if="includeAll">Showing every artist.</template>
      <template v-else>Showing artists with at least {{ model.minSongs }} songs.</template>
      <a :href="toggleAllUrl" class="ms-1">{{
        includeAll ? "Hide artists with only one song" : "Include artists with only one song"
      }}</a>
    </p>

    <ul v-if="model.artists.length" class="artist-list list-unstyled">
      <li v-for="artist in model.artists" :key="artist.name">
        <a :href="artistPageUrl(artist.name)">{{ artist.name }}</a>
        <small class="text-body-secondary"> ({{ artist.songs }})</small>
      </li>
    </ul>
    <p v-else>No artists found.</p>
  </PageFrame>
</template>

<style scoped>
.artist-list {
  columns: 16rem auto;
  column-gap: 2rem;
}

.artist-list li {
  break-inside: avoid;
}
</style>
