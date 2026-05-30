<script setup lang="ts">
import { computed, ref } from "vue";
import { TypedJSON } from "typedjson";
import type { BvEvent, TableFieldRaw } from "bootstrap-vue-next";
import { SearchesPageModel, SearchSummary } from "./SearchesPageModel";

declare const model_: string;

const model: SearchesPageModel = TypedJSON.parse(model_, SearchesPageModel)!;
const pageTitle = computed(() => (model.user === "all" ? "All Searches" : "My Searches"));
// --- URL builders ---
function pageUrl(p: number): string {
  const params = new URLSearchParams();
  if (model.user) params.set("user", model.user);
  if (model.sort) params.set("sort", model.sort);
  if (model.showDetails) params.set("showDetails", "true");
  if (model.spotifyOnly) params.set("spotifyOnly", "true");
  params.set("page", String(p));
  return "/Searches/Index?" + params.toString();
}

function sortUrl(newSort: string): string {
  const params = new URLSearchParams();
  if (model.user) params.set("user", model.user);
  if (newSort) params.set("sort", newSort);
  if (model.showDetails) params.set("showDetails", "true");
  if (model.spotifyOnly) params.set("spotifyOnly", "true");
  return "/Searches/Index?" + params.toString();
}

function spotifyToggleUrl(): string {
  const params = new URLSearchParams();
  if (model.user) params.set("user", model.user);
  if (model.sort) params.set("sort", model.sort);
  if (model.showDetails) params.set("showDetails", "true");
  if (!model.spotifyOnly) params.set("spotifyOnly", "true");
  return "/Searches/Index?" + params.toString();
}

function toggleDetailsUrl(): string {
  const params = new URLSearchParams();
  if (model.user) params.set("user", model.user);
  if (model.sort) params.set("sort", model.sort);
  if (!model.showDetails) params.set("showDetails", "true");
  if (model.spotifyOnly) params.set("spotifyOnly", "true");
  return "/Searches/Index?" + params.toString();
}

// --- Table fields ---
const fields = computed<Exclude<TableFieldRaw<SearchSummary>, string>[]>(() => {
  const base: Exclude<TableFieldRaw<SearchSummary>, string>[] = [
    { key: "search" as keyof SearchSummary, label: "Search", sortable: false },
  ];
  if (model.showDetails) {
    return [
      ...base,
      { key: "query" as keyof SearchSummary, label: "Query", sortable: false },
      { key: "userName" as keyof SearchSummary, label: "User", sortable: false },
      { key: "count" as keyof SearchSummary, label: "Count", sortable: false },
      { key: "created" as keyof SearchSummary, label: "Created", sortable: false },
      { key: "modified" as keyof SearchSummary, label: "Modified", sortable: false },
    ];
  }
  return [
    ...base,
    {
      key: (model.sort === "recent" ? "modified" : "count") as keyof SearchSummary,
      label: model.sort === "recent" ? "Modified" : "Count",
      sortable: false,
    },
  ];
});

// --- Windowed pagination entries ---
const editPageNumber = ref(model.page);

function onPageClick(event: BvEvent, pageNum: number): void {
  event.preventDefault();
  window.location.href = pageUrl(pageNum);
}

function goToPage(): void {
  let page = editPageNumber.value;
  if (typeof page !== "number" || isNaN(page)) {
    editPageNumber.value = model.page;
    return;
  }
  page = Math.floor(page);
  if (page < 1) page = 1;
  if (page > model.totalPages) page = model.totalPages;
  editPageNumber.value = page;
  if (page !== model.page) {
    window.location.href = pageUrl(page);
  }
}

function formatDate(dateStr: string | undefined): string {
  if (!dateStr) return "";
  return new Date(dateStr).toLocaleDateString("en-US", {
    month: "numeric",
    day: "numeric",
    year: "numeric",
  });
}

function spotifyUrl(id: string): string {
  return "https://open.spotify.com/playlist/" + id;
}

function onSpotifyToggle(): void {
  window.location.href = spotifyToggleUrl();
}
</script>

<template>
  <PageFrame id="app" :title="pageTitle">
    <!-- Navigation links -->
    <div class="row mb-2">
      <p class="col-sm">
        <a id="saved-search" :href="model.basicSearchUrl">Basic Search</a>
      </p>
      <p class="col-sm text-end pe-4">
        <a id="advanced-search" :href="model.advancedSearchUrl">Advanced Search</a>
      </p>
    </div>

    <!-- Sort buttons -->
    <div class="row mb-3">
      <div class="btn-group col-sm" role="group" aria-label="Sort order">
        <a
          :href="sortUrl('')"
          role="button"
          :class="model.sort !== 'recent' ? 'btn btn-primary' : 'btn btn-outline-primary'"
        >
          Most Popular
        </a>
        <a
          :href="sortUrl('recent')"
          role="button"
          :class="model.sort === 'recent' ? 'btn btn-primary' : 'btn btn-outline-primary'"
        >
          Most Recent
        </a>
      </div>
    </div>

    <!-- Spotify Only switch — hidden for all-users admin view since Spotify links are
         user-scoped and the toggle would have no meaningful effect across all users -->
    <div v-if="model.user !== 'all'" class="row mb-3">
      <div class="col-sm">
        <div class="form-check form-switch">
          <input
            id="spotifyOnlySwitch"
            class="form-check-input"
            type="checkbox"
            role="switch"
            :checked="model.spotifyOnly"
            @change="onSpotifyToggle()"
          />
          <label class="form-check-label" for="spotifyOnlySwitch">Spotify Only</label>
        </div>
      </div>
    </div>

    <!-- Search table -->
    <BTable
      id="searches-table"
      :fields="fields"
      :items="model.searches"
      striped
      class="table-songs col-sm"
    >
      <template #cell(search)="{ item }">
        <a :href="item.searchUrl" role="button" class="btn btn-success btn-sm">Search</a>
        <a
          v-if="item.searchPageUrl && item.mostRecentPage"
          :href="item.searchPageUrl"
          role="button"
          class="btn btn-outline-success btn-sm ms-1"
        >
          Page {{ item.mostRecentPage }}
        </a>
        &nbsp;
        <a :href="item.deleteUrl" role="button" class="btn btn-danger btn-sm">Delete</a>
        &nbsp;
        <a v-if="item.spotify" :href="spotifyUrl(item.spotify)" target="_blank">
          <img
            :src="'/images/icons/spotify-logo.png'"
            alt="Spotify Playlist"
            width="24"
            height="24"
          />
        </a>
        {{ item.description }}
      </template>
      <template #cell(created)="{ item }">{{ formatDate(item.created) }}</template>
      <template #cell(modified)="{ item }">{{ formatDate(item.modified) }}</template>
    </BTable>

    <!-- Admin controls -->
    <div v-if="model.isAdmin" class="mt-2">
      <a :href="toggleDetailsUrl()">Toggle Details</a>
    </div>

    <!-- Delete All -->
    <div v-if="model.canDeleteAll && model.deleteAllUrl" class="row mt-2">
      <div class="col-sm">
        <a :href="model.deleteAllUrl" class="btn btn-outline-danger btn-sm"
          >Clear My Search History</a
        >
      </div>
    </div>

    <!-- Pagination -->
    <BRow v-if="model.totalPages > 1" class="mt-3">
      <BCol md="8">
        <BPagination
          v-model="editPageNumber"
          :total-rows="model.totalPages"
          :per-page="1"
          limit="9"
          first-number
          last-number
          aria-label="Search history pages"
          @page-click="onPageClick"
        />
      </BCol>
      <BCol md="4">
        Page
        <input
          v-model.number="editPageNumber"
          type="number"
          step="1"
          :min="1"
          :max="model.totalPages"
          class="form-control form-control-sm d-inline-block"
          style="width: 4em"
          aria-label="Page number"
          @keydown.enter="goToPage"
          @blur="goToPage"
        />
        of {{ model.totalPages }}
      </BCol>
    </BRow>
  </PageFrame>
</template>
