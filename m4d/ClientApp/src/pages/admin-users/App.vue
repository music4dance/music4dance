<script setup lang="ts">
import { computed, ref } from "vue";
import { TypedJSON } from "typedjson";
import type { TableFieldRaw, BTableSortBy } from "bootstrap-vue-next";
import { AdminUsersModel, AdminUserSummary } from "./AdminUsersModel";

declare const model_: string;

const model: AdminUsersModel = TypedJSON.parse(model_, AdminUsersModel)!;

// --- Filter state ---
// Each user falls into exactly one of these categories; "Basic" is whatever's
// left over once Unconfirmed/Pseudo/Premium are excluded. Every category has
// its own show/hide checkbox, so unchecking "Basic" isolates whichever special
// categories remain checked (e.g. Pseudo alone, or Premium alone).
// "Private" is a separate AND-filter (not a category) that narrows the above
// by the privacy flag; checked (default) shows everyone, unchecked hides
// anyone whose privacy isn't fully public (255).
const showBasic = ref(true);
const showPremium = ref(true);
const showPrivate = ref(true);
const showUnconfirmed = ref(false);
const showPseudo = ref(false);
const userSearch = ref("");

// Mirrors UserMapper.GetPremiumUsers: premium role or a positive lifetime purchase total.
const PREMIUM_ROLE = "premium";
function isPremiumUser(u: AdminUserSummary): boolean {
  return u.roles.includes(PREMIUM_ROLE) || u.lifetimePurchased > 0;
}
function isUnconfirmedUser(u: AdminUserSummary): boolean {
  return !u.isPseudo && !u.emailConfirmed;
}
function isBasicUser(u: AdminUserSummary): boolean {
  return !u.isPseudo && !isUnconfirmedUser(u) && !isPremiumUser(u);
}

// --- Pagination ---
const perPage = ref(50);
const currentPage = ref(1);

// --- Sort ---
const sortBy = ref<BTableSortBy[]>([{ key: "lastActive", order: "desc" }]);

// --- Never-active sentinel (1900-01-01) ---
const NEVER_ACTIVE = new Date(1900, 0, 1).getFullYear();

function isNeverActive(dateStr: string): boolean {
  return new Date(dateStr).getFullYear() === NEVER_ACTIVE;
}

function formatDate(dateStr: string): string {
  if (isNeverActive(dateStr)) return "Never";
  return new Date(dateStr).toLocaleDateString("en-US", {
    month: "numeric",
    day: "numeric",
    year: "numeric",
  });
}

// --- Computed: unfiltered users (all users, used for summary stats) ---
const allUsers = computed(() => model.users ?? []);

// --- Computed: filtered users for the table ---
const filteredUsers = computed(() => {
  const sp = showPrivate.value;
  const q = userSearch.value.trim().toLowerCase();

  return allUsers.value.filter((u) => {
    const inCategory =
      (showBasic.value && isBasicUser(u)) ||
      (showUnconfirmed.value && isUnconfirmedUser(u)) ||
      (showPseudo.value && u.isPseudo) ||
      (showPremium.value && isPremiumUser(u));
    if (!inCategory) return false;
    if (!sp && u.privacy !== 255) return false;
    if (q && !u.userName.toLowerCase().includes(q) && !(u.email ?? "").toLowerCase().includes(q))
      return false;
    return true;
  });
});

// --- Summary stats (always computed from allUsers) ---
const totalUsers = computed(() => allUsers.value.length);
const registeredUsers = computed(() => allUsers.value.filter((u) => !u.isPseudo).length);
const confirmedUsers = computed(
  () => allUsers.value.filter((u) => u.emailConfirmed && !u.isPseudo).length,
);
const deletedUsers = computed(
  () => allUsers.value.filter((u) => u.userName.startsWith("DEL:")).length,
);

const roleStats = computed(() =>
  (model.allRoles ?? []).map((role) => ({
    role,
    count: allUsers.value.filter((u) => u.roles.includes(role)).length,
  })),
);

const loginStats = computed(() => {
  const knownLogins = ["Microsoft", "Spotify", "Facebook", "Google"];
  return knownLogins.map((login) => ({
    login,
    count: allUsers.value.filter((u) => u.logins.includes(login)).length,
  }));
});

const serviceStats = computed(() => {
  const registeredCount = registeredUsers.value;
  const withPreference = allUsers.value.filter((u) => !u.isPseudo && u.servicePreference).length;

  const rows = (model.services ?? [])
    .map((svc) => {
      const count = allUsers.value.filter(
        (u) => u.servicePreference?.includes(svc.cid) ?? false,
      ).length;
      return {
        name: svc.name,
        count,
        pctUsers: registeredCount > 0 ? count / registeredCount : 0,
        pctPref: withPreference > 0 ? count / withPreference : 0,
      };
    })
    .filter((r) => r.count > 0);

  const noneCount = registeredCount - withPreference;
  return {
    rows,
    noneCount,
    nonePercent: registeredCount > 0 ? noneCount / registeredCount : 0,
  };
});

// --- Table field definitions ---
const fields: Exclude<TableFieldRaw<AdminUserSummary>, string>[] = [
  {
    key: "emailConfirmed",
    label: "EC",
    sortable: true,
  },
  {
    key: "userName",
    label: "UserName / Email",
    sortable: true,
  },
  {
    key: "startDate",
    label: "Signed Up",
    sortable: true,
    sortByFormatted: true,
    formatter: ({ item }: { value: unknown; key: string; item: AdminUserSummary }) =>
      new Date(item.startDate).getTime().toString().padStart(20, "0"),
  },
  {
    key: "lastActive",
    label: "Signed In",
    sortable: true,
    sortByFormatted: true,
    formatter: ({ item }: { value: unknown; key: string; item: AdminUserSummary }) =>
      new Date(item.lastActive).getTime().toString().padStart(20, "0"),
  },
  {
    key: "privacy",
    label: "PRV",
    sortable: true,
  },
  {
    key: "canContact",
    label: "Contact Prefs",
    sortable: true,
  },
  {
    key: "servicePreference",
    label: "Services",
    sortable: true,
  },
  {
    key: "failedCardAttempts",
    label: "CCF",
    sortable: true,
  },
  {
    key: "lifetimePurchased",
    label: "$",
    sortable: true,
  },
  {
    key: "hitCount",
    label: "HC",
    sortable: true,
  },
  { key: "roles", label: "Roles", sortable: false },
  { key: "logins", label: "Logins", sortable: false },
  { key: "actions", label: "", sortable: false },
];

function fmt2(n: number): string {
  return (n * 100).toFixed(0) + "%";
}

function getContactPreferences(canContact: number): string[] {
  if (!canContact) {
    return ["None"];
  }

  const prefs: string[] = [];
  if ((canContact & 0x01) === 0x01) {
    prefs.push("music4dance promo");
  }
  if ((canContact & 0x02) === 0x02) {
    prefs.push("partner promo");
  }
  if ((canContact & 0x04) === 0x04) {
    prefs.push("surveys/blog");
  }

  return prefs.length > 0 ? prefs : ["None"];
}

function getServicePreferences(servicePreference?: string): string[] {
  if (!servicePreference) {
    return ["None"];
  }

  const names = servicePreference
    .split("")
    .map((cid) => (model.services ?? []).find((svc) => svc.cid === cid)?.name ?? cid);

  return names.length > 0 ? names : ["None"];
}

// Build action URLs in JS (not as template literals) so `&` is never in HTML.
function listSongsUrl(userName: string): string {
  return "/Song/FilterUser?user=" + encodeURIComponent(userName);
}
function listSearchesUrl(userName: string): string {
  return "/Searches/Index?user=" + encodeURIComponent(userName) + "&showDetails=true&sort=recent";
}
function usageUrl(userName: string): string {
  return "/UsageLog/UserLog?user=" + encodeURIComponent(userName);
}
function playlistsUrl(userName: string): string {
  return "/PlayList/Index?user=" + encodeURIComponent(userName);
}
function mergeUrl(userName: string): string {
  return "/ApplicationUsers/Merge?mergeName=" + encodeURIComponent(userName);
}
</script>

<template>
  <PageFrame id="app" title="User Administrator">
    <!-- Summary row -->
    <div class="row mb-3">
      <div class="col-md-2">
        <p><strong>Total Users:</strong> {{ totalUsers }}</p>
        <p><strong>Registered Users:</strong> {{ registeredUsers }}</p>
        <p><strong>Confirmed Users:</strong> {{ confirmedUsers }}</p>
        <p><strong>Deleted Users:</strong> {{ deletedUsers }}</p>
      </div>

      <div class="col-md-2">
        <p v-for="{ role, count } in roleStats" :key="role">
          <b>{{ role }}</b
          >: {{ count }}
        </p>
      </div>

      <div class="col-md-2">
        <p v-for="{ login, count } in loginStats" :key="login">
          <b>{{ login }}</b
          >: {{ count }}
        </p>
      </div>

      <div class="col-md-3">
        <p class="mb-2">
          <a href="/ApplicationUsers/Create" class="btn btn-primary" role="button"
            >New Pseudo User</a
          >
        </p>
        <p class="mb-2">
          <a href="/ApplicationUsers/ClearCache" class="btn btn-secondary" role="button"
            >Clear Cache</a
          >
        </p>
        <p class="mb-2">
          <a href="/ApplicationUsers/VotingResults" class="btn btn-primary" role="button"
            >Voting Results</a
          >
        </p>
        <p class="mb-0">
          <a href="/ApplicationUsers/DeleteUnconfirmed" class="btn btn-danger" role="button"
            >Delete Unconfirmed</a
          >
        </p>
      </div>

      <div class="col-md-3">
        <p class="mb-1"><strong>Show users:</strong></p>
        <BFormCheckbox v-model="showBasic" @update:model-value="currentPage = 1">
          Basic
        </BFormCheckbox>
        <BFormCheckbox v-model="showPremium" @update:model-value="currentPage = 1">
          Premium
        </BFormCheckbox>
        <BFormCheckbox v-model="showPrivate" @update:model-value="currentPage = 1">
          Private
        </BFormCheckbox>
        <BFormCheckbox v-model="showUnconfirmed" @update:model-value="currentPage = 1">
          Unconfirmed
        </BFormCheckbox>
        <BFormCheckbox v-model="showPseudo" @update:model-value="currentPage = 1">
          Pseudo
        </BFormCheckbox>
      </div>
    </div>

    <!-- Service preference table -->
    <table class="table table-striped mb-3">
      <thead>
        <tr>
          <th>Service</th>
          <th>Users</th>
          <th>%u</th>
          <th>%p</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in serviceStats.rows" :key="row.name">
          <td>{{ row.name }}</td>
          <td>{{ row.count }}</td>
          <td>{{ fmt2(row.pctUsers) }}</td>
          <td>{{ fmt2(row.pctPref) }}</td>
        </tr>
        <tr>
          <td>none</td>
          <td>{{ serviceStats.noneCount }}</td>
          <td>{{ fmt2(serviceStats.nonePercent) }}</td>
          <td />
        </tr>
      </tbody>
    </table>

    <hr />
    <h3>Users ({{ filteredUsers.length }})</h3>

    <!-- Text search -->
    <div class="mb-2" style="max-width: 24rem">
      <BFormInput
        v-model="userSearch"
        placeholder="Filter by name or email…"
        size="sm"
        clearable
        @update:model-value="currentPage = 1"
      />
    </div>

    <!-- Pagination controls (top) -->
    <div class="d-flex align-items-center gap-3 mb-2">
      <BPagination
        v-model="currentPage"
        :total-rows="filteredUsers.length"
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

    <!-- User table -->
    <BTable
      id="users-table"
      v-model:sort-by="sortBy"
      striped
      hover
      responsive
      small
      primary-key="id"
      :items="filteredUsers"
      :fields="fields"
      :per-page="perPage"
      :current-page="currentPage"
      sort-icon-left
    >
      <!-- EC column -->
      <template #cell(emailConfirmed)="{ item }">
        <IBiCheckCircleFill v-if="item.emailConfirmed" class="text-success" />
        <IBiCircle v-else class="text-muted" />
      </template>

      <!-- UserName / Email -->
      <template #cell(userName)="{ item }">
        <a
          :href="`/ApplicationUsers/Details/${item.id}`"
          :style="item.isPseudo ? 'font-style:italic' : ''"
          >{{ item.userName }}</a
        >
        <br />
        <small class="text-muted">{{ item.email }}</small>
      </template>

      <!-- Signed Up -->
      <template #cell(startDate)="{ item }">
        {{ formatDate(item.startDate) }}
      </template>

      <!-- Signed In -->
      <template #cell(lastActive)="{ item }">
        {{ formatDate(item.lastActive) }}
      </template>

      <!-- Contact preferences -->
      <template #cell(canContact)="{ item }">
        <span v-for="(preference, i) in getContactPreferences(item.canContact)" :key="preference">
          <br v-if="i > 0" />{{ preference }}
        </span>
      </template>

      <!-- Preferred services -->
      <template #cell(servicePreference)="{ item }">
        <span
          v-for="(serviceName, i) in getServicePreferences(item.servicePreference)"
          :key="serviceName"
        >
          <br v-if="i > 0" />{{ serviceName }}
        </span>
      </template>

      <!-- Roles -->
      <template #cell(roles)="{ item }">
        <span v-for="(role, i) in item.roles" :key="role"> <br v-if="i > 0" />{{ role }} </span>
      </template>

      <!-- Logins -->
      <template #cell(logins)="{ item }">
        <span v-for="(login, i) in item.logins" :key="login"> <br v-if="i > 0" />{{ login }} </span>
      </template>

      <!-- Actions -->
      <template #cell(actions)="{ item }">
        <a :href="listSongsUrl(item.userName)">List Songs</a>,
        <a :href="listSearchesUrl(item.userName)">List Searches</a>
        <br />
        <a :href="`/ApplicationUsers/ChangeRoles/${item.id}`">Change Roles</a>
        <br />
        <a :href="`/ApplicationUsers/Delete/${item.id}`">Delete</a>,
        <a :href="`/ApplicationUsers/Edit/${item.id}`">Edit</a>,
        <a v-if="item.isPseudo" :href="mergeUrl(item.userName)">Merge</a>,
        <a :href="usageUrl(item.userName)">Usage</a>,
        <a :href="playlistsUrl(item.userName)">Playlists</a>,
        <a :href="`/ApplicationUsers/ClearPremium/${item.id}`">ClearPremium</a>
      </template>
    </BTable>

    <!-- Pagination controls (bottom) -->
    <BPagination
      v-model="currentPage"
      :total-rows="filteredUsers.length"
      :per-page="perPage"
      size="sm"
    />
  </PageFrame>
</template>
