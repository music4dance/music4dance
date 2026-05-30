<script setup lang="ts">
import { computed, ref } from "vue";
import { TypedJSON } from "typedjson";
import type { TableFieldRaw, BTableSortBy } from "bootstrap-vue-next";
import { PlayListPageModel, PlayListSummary } from "./PlayListPageModel";

declare const model_: string;

const model: PlayListPageModel = TypedJSON.parse(model_, PlayListPageModel)!;

// PlayListType enum values (mirrors C# PlayListType)
const SONGS_FROM_SPOTIFY = 2;
const SPOTIFY_FROM_SEARCH = 3;

// --- Filter state ---
const showDeleted = ref(false);
const userFilter = ref(model.filteredUser ?? "");

// --- Pagination ---
const perPage = ref(50);
const currentPage = ref(1);

// --- Sort ---
const sortBy = ref<BTableSortBy[]>([{ key: "updated", order: "desc" }]);

// --- Helpers ---
function formatDate(dateStr: string | undefined): string {
  if (!dateStr) return "";
  return new Date(dateStr).toLocaleDateString("en-US", {
    month: "numeric",
    day: "numeric",
    year: "numeric",
  });
}

function playListTypeName(type: number): string {
  if (type === SONGS_FROM_SPOTIFY) return "SongsFromSpotify";
  if (type === SPOTIFY_FROM_SEARCH) return "SpotifyFromSearch";
  return "Unknown";
}

// --- Computed ---
const allPlaylists = computed(() => model.playLists ?? []);

const hasData2 = computed(() => model.data2Name !== "Data2");

const filteredPlaylists = computed(() => {
  const sd = showDeleted.value;
  const q = userFilter.value.trim().toLowerCase();

  return allPlaylists.value.filter((p) => {
    if (p.deleted !== sd) return false;
    if (
      q &&
      !p.user.toLowerCase().includes(q) &&
      !(p.name ?? "").toLowerCase().includes(q) &&
      !(p.description ?? "").toLowerCase().includes(q) &&
      !(p.data1 ?? "").toLowerCase().includes(q) &&
      !p.id.toLowerCase().includes(q)
    )
      return false;
    return true;
  });
});

const activeCount = computed(() => allPlaylists.value.filter((p) => !p.deleted).length);
const deletedCount = computed(() => allPlaylists.value.filter((p) => p.deleted).length);

// --- Table fields (computed because showDeleted affects columns) ---
const fields = computed<Exclude<TableFieldRaw<PlayListSummary>, string>[]>(() => [
  { key: "id", label: "Id", sortable: true },
  { key: "user", label: "User", sortable: true },
  { key: "name", label: "Name", sortable: true },
  { key: "description", label: "Description", sortable: false },
  { key: "data1", label: model.data1Name, sortable: false },
  ...(hasData2.value ? [{ key: "data2", label: model.data2Name, sortable: false }] : []),
  {
    key: "created",
    label: "Created",
    sortable: true,
    sortByFormatted: true,
    formatter: ({ item }: { value: unknown; key: string; item: PlayListSummary }) =>
      new Date(item.created).getTime().toString().padStart(20, "0"),
  },
  {
    key: "updated",
    label: "Updated",
    sortable: true,
    sortByFormatted: true,
    formatter: ({ item }: { value: unknown; key: string; item: PlayListSummary }) =>
      item.updated
        ? new Date(item.updated).getTime().toString().padStart(20, "0")
        : "0".padStart(20, "0"),
  },
  ...(showDeleted.value ? [{ key: "deletedAt", label: "Deleted", sortable: false }] : []),
  { key: "actions", label: "", sortable: false },
]);

// --- URL builders (avoid & in HTML templates) ---
function indexUrl(type: number, user?: string): string {
  let url = "/PlayList/Index?type=" + type;
  if (user) url += "&user=" + encodeURIComponent(user);
  return url;
}

function updateAllUrl(): string {
  return "/PlayList/UpdateAll?type=" + model.type;
}

function deleteAllUrl(): string {
  return "/PlayList/DeleteAll?user=" + encodeURIComponent(userFilter.value) + "&type=" + model.type;
}

function deleteUrl(id: string): string {
  const u = userFilter.value;
  return "/PlayList/Delete/" + id + (u ? "?user=" + encodeURIComponent(u) : "");
}

function undeleteUrl(id: string): string {
  const u = userFilter.value;
  return "/PlayList/Undelete/" + id + (u ? "?user=" + encodeURIComponent(u) : "");
}

function rowClass(item: PlayListSummary): string {
  return item.deleted ? "table-danger" : "";
}
</script>

<template>
  <div class="container-fluid">
    <h2>{{ playListTypeName(model.type) }} Playlists</h2>

    <!-- User filter banner -->
    <div v-if="userFilter" class="alert alert-info d-flex align-items-center gap-2 py-2">
      <span
        >Filtered to user: <strong>{{ userFilter }}</strong></span
      >
      <BButton
        size="sm"
        variant="outline-secondary"
        @click="
          userFilter = '';
          currentPage = 1;
        "
      >
        Clear filter
      </BButton>
    </div>

    <div class="row mb-3">
      <!-- Sidebar: actions -->
      <div class="col-sm-3">
        <p>
          <a href="/PlayList/Create" class="btn btn-primary btn-sm">Create New</a>
        </p>
        <p v-if="model.type === SONGS_FROM_SPOTIFY">
          <a href="/PlayList/RestoreAll" class="btn btn-outline-secondary btn-sm">Restore All</a>
        </p>
        <p>
          <a :href="updateAllUrl()" class="btn btn-outline-secondary btn-sm">Update All</a>
        </p>
        <p>
          <BButton
            size="sm"
            :variant="showDeleted ? 'secondary' : 'outline-secondary'"
            @click="
              showDeleted = !showDeleted;
              currentPage = 1;
            "
          >
            {{ showDeleted ? "Show Active" : "Show Deleted" }}
          </BButton>
        </p>
        <p v-if="userFilter && !showDeleted && filteredPlaylists.length > 0">
          <a :href="deleteAllUrl()" class="btn btn-danger btn-sm">Delete All</a>
        </p>
      </div>

      <!-- Type switcher -->
      <div class="col-sm-3">
        <p><strong>Type:</strong></p>
        <p>
          <a
            :class="model.type === SONGS_FROM_SPOTIFY ? 'fw-bold' : ''"
            :href="indexUrl(SONGS_FROM_SPOTIFY, userFilter || undefined)"
            >SongsFromSpotify</a
          >
        </p>
        <p>
          <a
            :class="model.type === SPOTIFY_FROM_SEARCH ? 'fw-bold' : ''"
            :href="indexUrl(SPOTIFY_FROM_SEARCH, userFilter || undefined)"
            >SpotifyFromSearch</a
          >
        </p>
      </div>

      <!-- BulkCreate (SpotifyFromSearch only) + Statistics -->
      <div v-if="model.type === SPOTIFY_FROM_SEARCH" class="col-sm-3">
        <p><strong>Bulk Create:</strong></p>
        <p><a href="/PlayList/BulkCreate?flavor=TopN">Create TopN</a></p>
        <p><a href="/PlayList/BulkCreate?flavor=Holiday">Create Holiday</a></p>
        <p><a href="/PlayList/BulkCreate?flavor=Halloween">Create Halloween</a></p>
        <p><a href="/PlayList/Statistics">Statistics</a></p>
      </div>

      <!-- Stats -->
      <div class="col-sm-3">
        <p><strong>Active:</strong> {{ activeCount }}</p>
        <p><strong>Deleted:</strong> {{ deletedCount }}</p>
      </div>
    </div>

    <!-- Filter + Pagination controls -->
    <div class="d-flex align-items-center gap-3 mb-2 flex-wrap">
      <BFormInput
        v-model="userFilter"
        placeholder="Filter by user, name, description, tags or id…"
        size="sm"
        clearable
        style="max-width: 20rem"
        @update:model-value="currentPage = 1"
      />
      <BPagination
        v-model="currentPage"
        :total-rows="filteredPlaylists.length"
        :per-page="perPage"
        size="sm"
        class="mb-0"
      />
      <div class="d-flex align-items-center gap-2">
        <label class="mb-0 small">Per page:</label>
        <BFormSelect
          v-model="perPage"
          :options="[25, 50, 100, 250]"
          size="sm"
          style="width: auto"
          @change="currentPage = 1"
        />
      </div>
    </div>

    <p class="text-muted small">
      Showing {{ filteredPlaylists.length }} {{ showDeleted ? "deleted" : "active" }} playlists
    </p>

    <!-- Playlist table -->
    <BTable
      id="playlist-table"
      v-model:sort-by="sortBy"
      striped
      hover
      responsive
      small
      primary-key="id"
      :items="filteredPlaylists"
      :fields="fields"
      :per-page="perPage"
      :current-page="currentPage"
      :tbody-tr-class="rowClass"
      sort-icon-left
    >
      <!-- Id column: link to Details -->
      <template #cell(id)="{ item }">
        <a :href="'/PlayList/Details/' + item.id">{{ item.id }}</a>
      </template>

      <!-- User column: click to filter -->
      <template #cell(user)="{ item }">
        <a
          href="#"
          @click.prevent="
            userFilter = item.user;
            currentPage = 1;
          "
          >{{ item.user }}</a
        >
      </template>

      <!-- Created column -->
      <template #cell(created)="{ item }">{{ formatDate(item.created) }}</template>

      <!-- Updated column -->
      <template #cell(updated)="{ item }">{{ formatDate(item.updated) }}</template>

      <!-- Actions column -->
      <template #cell(actions)="{ item }">
        <template v-if="item.deleted">
          <a :href="undeleteUrl(item.id)">Undelete</a>
        </template>
        <template v-else>
          <a :href="'/PlayList/Update/' + item.id">Update</a> |
          <a :href="'/PlayList/Edit/' + item.id">Edit</a> |
          <a :href="'/PlayList/Details/' + item.id">Details</a> |
          <a :href="deleteUrl(item.id)">Delete</a>
          <template v-if="item.updated && !item.data2">
            | <a :href="'/PlayList/Restore/' + item.id">Restore</a>
          </template>
        </template>
      </template>
    </BTable>
  </div>
</template>
