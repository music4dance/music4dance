<script setup lang="ts">
import DanceSelector from "@/components/DanceSelector.vue";
import KeywordEditor from "./components/KeywordEditor.vue";
import PageFrame from "@/components/PageFrame.vue";
import TagCategorySelector from "@/components/TagCategorySelector.vue";
import { DanceQuery } from "@/models/DanceQuery";
import { SongFilter } from "@/models/SongFilter";
import { SongSort, SortOrder } from "@/models/SongSort";
import { Tag } from "@/models/Tag";
import { UserQuery } from "@/models/UserQuery";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { safeTagDatabase } from "@/helpers/TagEnvironmentManager";
import { computed, ref } from "vue";
import type { DanceDatabase } from "@/models/DanceDatabase/DanceDatabase";

interface SortOption {
  text: string;
  value: SortOrder | null;
}

const context = getMenuContext();
const danceDB: DanceDatabase = safeDanceDatabase();

const allDances = danceDB.all;

const tagDatabase = safeTagDatabase();
const filter = getQueryFilter();
const danceQueryInit = new DanceQuery(filter.dances);

var showDiagnostics = getShowDiagnostics();
var keyWords = ref(filter.searchString ?? "");
var advancedText = ref(false);
var dances = ref(danceQueryInit.danceList);
var danceConnector = ref(danceQueryInit.isExclusive ? "all" : "any");

const tags: Tag[] = tagDatabase.tags;
var includeTags = ref(filter.tags ? extractTags(filter.tags, true) : []);
var excludeTags = ref(filter.tags ? extractTags(filter.tags, false) : []);

var tempoMin = ref(filter.tempoMin ?? 0);
var tempoMax = ref(filter.tempoMax ?? 400);

var lengthMin = ref(filter.lengthMin ?? 0);
var lengthMax = ref(filter.lengthMax ?? 600);

const userQueryInit = new UserQuery(filter.user);

var user = ref(userQueryInit.userName ?? context.userName ?? "");
var displayUser = ref(userQueryInit.displayName);

var bonuses = ref(computeBonuses());
var validated = ref(false);
var services = ref(filter.purchase ? filter.purchase.trim().split("") : []);
var activity = ref(userQueryInit.parts);

var sortOptions: SortOption[] = [
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
var sortInit = new SongSort(filter.sortOrder);
var sort = ref(sortInit.order ?? null);
var order = ref(sortInit.direction);

const danceNames = computed(() => {
  return dances.value.map((d) => danceDB.danceFromId(d)!.name);
});

const activities = computed(() => {
  const user = displayUser;
  const empty = { text: "Don't filter on user activity", value: "NT" };

  if (!user.value) {
    return [empty];
  }

  const my = user.value === "me" ? "my" : user.value + "'s";
  const i = user.value === "me" ? "I have" : user.value + " has";

  const items = [
    { text: `Include all songs in ${my} favorites`, value: "IL" },
    { text: `Exclude all songs in ${my} favorites`, value: "XL" },
    { text: `Include all songs ${i} tagged`, value: "IT" },
    { text: `Exclude all songs ${i} tagged`, value: "XT" },
    { text: `Exclude all songs in ${my} blocked list`, value: "XH" },
    { text: `Include all songs in ${my} blocked list`, value: "IH" },
  ];

  if (dances.value.length > 0) {
    items.unshift({
      text: `Include all songs ${i} voted against ${danceNames.value.join(", ")}`,
      value: "IX",
    });
    items.unshift({
      text: `Include all songs ${i} voted for ${danceNames.value.join(", ")}`,
      value: "ID",
    });
  }
  items.unshift(empty);
  return items;
});

const computedActivity = computed({
  get: function (): string {
    return displayUser.value ? activity.value : "NT";
  },
  set: function (value: string): void {
    if (displayUser.value) {
      activity.value = value;
    }
  },
});

const isAnonymous = computed(() => {
  return displayUser.value === "Anonymous";
});

function getQueryFilter(): SongFilter {
  const params = new URLSearchParams(window.location.search);
  const filterString = params.get("filter");

  return filterString ? SongFilter.buildFilter(filterString) : new SongFilter();
}

function getShowDiagnostics(): boolean {
  const params = new URLSearchParams(window.location.search);
  const showDiagnostics = params.get("showDiagnostics");

  return !!showDiagnostics && showDiagnostics !== "false";
}

const buildSingleTagList = (tags: string[], decorator: string): string => {
  return tags.map((t) => `${decorator}${t}`).join("|");
};

const tagList = computed(() => {
  const lists: string[] = [];
  const include = includeTags.value;
  if (include.length > 0) {
    lists.push(buildSingleTagList(include, "+"));
  }
  const exclude = excludeTags.value;
  if (exclude.length > 0) {
    lists.push(buildSingleTagList(exclude, "-"));
  }
  return lists.join("|");
});

const songFilter = computed(() => {
  const danceQuery = DanceQuery.fromParts(dances.value, danceConnector.value === "all");
  const userQuery = UserQuery.fromParts(
    computedActivity.value ? computedActivity.value : undefined,
    isAnonymous.value ? user.value : displayUser.value,
  );
  const filter = new SongFilter();
  let level = 0;
  if (bonuses.value.indexOf("P") !== -1) {
    level = 1;
  }
  if (bonuses.value.indexOf("D") !== -1) {
    level += 2;
  }

  filter.action = "Advanced";
  filter.searchString = keyWords.value;
  filter.dances = danceQuery.query;
  filter.sortOrder = SongSort.fromParts(sort.value ?? undefined, order.value).query;
  filter.user = userQuery.query;
  filter.purchase = services.value.join("");
  filter.tempoMin = tempoMin.value === 0 ? undefined : tempoMin.value;
  filter.tempoMax = tempoMax.value >= 400 ? undefined : tempoMax.value;
  filter.lengthMin = lengthMin.value === 0 ? undefined : lengthMin.value;
  filter.lengthMax = lengthMax.value >= 400 ? undefined : lengthMax.value;
  filter.tags = tagList.value;
  filter.level = level ? level : undefined;

  return filter;
});

const validSortOptions = computed(() => {
  const singleDance = songFilter.value.singleDance;
  return sortOptions.filter((opt) => opt.value !== SortOrder.Dances || singleDance);
});

function computeBonuses(): string[] {
  const bonuses = [];
  if (filter.level && filter.level & 1) {
    bonuses.push("P");
  }
  if (filter.level && filter.level & 2) {
    bonuses.push("D");
  }
  return bonuses;
}

function extractTags(tags: string, include: boolean): string[] {
  if (!tags) {
    return [];
  }

  const qualifier = include ? "+" : "-";
  const parts = tags.split("|").map((p) => p.trim());
  let filtered = parts.filter((p) => p.startsWith(qualifier)).map((p) => p.slice(1));
  if (include) {
    filtered = filtered.concat(parts.filter((p) => !p.startsWith("+") && !p.startsWith("-")));
  }

  return filtered;
}

async function onSubmit(): Promise<void> {
  const form = document.getElementById("advanced-search") as HTMLFormElement;

  if (form.checkValidity() === true) {
    const min = tempoMin.value;
    const max = tempoMax.value;
    if (min > max) {
      const tempo = max;
      tempoMax.value = min;
      tempoMin.value = tempo;
    }

    if (lengthMin.value > lengthMax.value) {
      const length = lengthMax.value;
      lengthMax.value = lengthMin.value;
      lengthMin.value = length;
    }

    const loc = window.location;
    const query = songFilter.value.encodedQuery;

    const state = window.location.pathname + `?filter=${query}`;
    window.history.replaceState(null, "", state);

    window.location.href = `${loc.origin}/song/filtersearch?filter=${query}`;
  }

  validated.value = true;
}

function onReset(evt: Event): void {
  evt.preventDefault();
  const userName = isAnonymous.value ? user : displayUser;

  keyWords.value = "";
  advancedText.value = false;
  dances.value = [];
  danceConnector.value = "any";
  includeTags.value = [];
  excludeTags.value = [];
  tempoMin.value = 0;
  tempoMax.value = 400;
  lengthMin.value = 0;
  lengthMax.value = 600;
  if (userName) {
    user.value = userName.value;
    activity.value = "IH";
  } else {
    user.value = "";
    activity.value = "NT";
  }
  displayUser.value = "";
  services.value = [];
  sort.value = null;
  order.value = "asc";
  bonuses.value = [];

  validated.value = false;
}
</script>

<template>
  <PageFrame id="app">
    <h1 class="col-sm-12" style="font-size: 22px; text-align: center">Advanced Song Search</h1>
    <div style="max-width: 600px; margin-left: auto; margin-right: auto">
      <BForm
        id="advanced-search"
        :validated="validated"
        novalidate
        @submit.stop.prevent="onSubmit"
        @reset="onReset"
      >
        <KeywordEditor
          id="search-string-group"
          v-model="keyWords"
          v-model:advanced="advancedText"
        />

        <BFormGroup id="dance-group" label="Dances:">
          <div style="border: 1px solid #ced4da; boder-radius: 0.25rem">
            <DanceSelector
              id="dance-selector"
              v-model="dances"
              :dance-list="allDances"
            ></DanceSelector>
            <div class="d-flex justify-content-between w-100 mx-1 mb-2">
              <BFormRadioGroup
                id="dance-connector"
                v-model="danceConnector"
                buttons
                button-variant="outline-secondary"
                :disabled="dances.length < 2"
                class="mx-3"
                size="sm"
              >
                <BFormRadio value="any">Any</BFormRadio>
                <BFormRadio value="all">All</BFormRadio>
              </BFormRadioGroup>
            </div>
          </div>
        </BFormGroup>

        <BFormGroup id="include-tags-group" label="Include Tags:">
          <TagCategorySelector
            id="include-tags"
            v-model="includeTags"
            :tag-list="tags"
            choose-label="Choose Tags to Include"
            search-label="Search Tags"
            empty-label="No more tags to choose"
          ></TagCategorySelector>
        </BFormGroup>

        <BFormGroup id="exclude-tags-group" label="Exclude Tags:">
          <TagCategorySelector
            id="exclude-tags"
            v-model="excludeTags"
            :tag-list="tags"
            choose-label="Choose Tags to Exclude"
            search-label="Search Tags"
            empty-label="No more tags to choose"
          ></TagCategorySelector>
        </BFormGroup>

        <BFormGroup id="tempo-range-group" label="Tempo range (BPM):" label-for="tempo-range">
          <BFormGroup id="tempo-range">
            <div class="d-flex">
              <BFormInput
                id="tempo-min"
                v-model="tempoMin"
                type="number"
                number
                min="0"
                max="400"
                style="width: 6rem"
              ></BFormInput>
              <span class="mx-2 pt-2">to</span>
              <BFormInput
                id="tempo-max"
                v-model="tempoMax"
                type="number"
                min="0"
                max="400"
                number
                style="width: 6rem"
              ></BFormInput>
              <div class="invalid-feedback">Tempos must be between 0 and 400 BPM</div>
            </div>
          </BFormGroup>
        </BFormGroup>

        <BFormGroup
          id="length-range-group"
          label="Length range (seconds):"
          label-for="length-range"
        >
          <BFormGroup id="length-range">
            <div class="d-flex">
              <BFormInput
                id="length-min"
                v-model="lengthMin"
                type="number"
                number
                min="0"
                max="600"
                style="width: 6rem"
              ></BFormInput>
              <span class="mx-2 pt-2">to</span>
              <BFormInput
                id="length-max"
                v-model="lengthMax"
                type="number"
                min="0"
                max="600"
                number
                style="width: 6rem"
              ></BFormInput>
              <div class="invalid-feedback">Length must be between 0 and 600 seconds</div>
            </div>
          </BFormGroup>
        </BFormGroup>

        <BFormGroup id="activity-group" label="By User:" label-for="activity">
          <div class="d-flex">
            <BFormInput
              id="user"
              v-model="displayUser"
              placeholder="UserName or me"
              style="width: 10rem"
              class="me-2"
            ></BFormInput>
            <BFormSelect
              id="activity"
              v-model="computedActivity"
              :options="activities"
            ></BFormSelect>
          </div>
        </BFormGroup>

        <BFormGroup
          id="services-group"
          class="mx-2 mb-2"
          label="Available on:"
          label-for="services"
        >
          <BFormCheckboxGroup id="services" v-model="services">
            <BFormCheckbox value="A">Amazon</BFormCheckbox>
            <BFormCheckbox value="I">ITunes</BFormCheckbox>
            <BFormCheckbox value="S">Spotify</BFormCheckbox>
          </BFormCheckboxGroup>
        </BFormGroup>

        <BFormGroup id="bonuses-group" class="mx-2 mb-2" label="Bonus content:" label-for="bonuses">
          <BFormCheckboxGroup id="bonuses" v-model="bonuses">
            <BFormCheckbox value="P">Not found in any publisher catalog</BFormCheckbox>
            <BFormCheckbox value="D">Not categorized by dance</BFormCheckbox>
          </BFormCheckboxGroup>
          <template #description>
            <a href="https://music4dance.blog/music4dance-help/subscriptions/">Premium content</a>
          </template>
        </BFormGroup>

        <BFormGroup id="sort-group" label="Sort By:" label-for="sort">
          <BFormSelect id="sort" v-model="sort" :options="validSortOptions" required></BFormSelect>
          <BFormRadioGroup id="sort-order" v-model="order" name="sort-order" class="mt-2">
            <BFormRadio value="asc"
              >Ascending (A-Z, Slow-Fast, Newest-Oldest, Shortest-Longest)</BFormRadio
            >
            <BFormRadio value="desc"
              >Descending (Z-A, Fast-Slow, Oldest-Newest, Longest-Shortest)</BFormRadio
            >
          </BFormRadioGroup>
        </BFormGroup>

        <div class="d-flex justify-content-between w-100 mx-1 mb-2">
          <BButton type="reset" variant="secondary">Reset</BButton>
          <BButton type="submit" variant="primary">Submit</BButton>
        </div>
      </BForm>
      <BCard v-if="showDiagnostics" class="mt-3" header="Form Data Result">
        <pre class="m-0">
searchString = {{ keyWords }}
dances = {{ dances }}
danceConnector = {{ danceConnector }}
tempoMin = {{ tempoMin }}
tempoMax = {{ tempoMax }}
lengthMin = {{ lengthMin }}
lengthMax = {{ lengthMax }}
activity = {{ computedActivity }}
services = {{ services }}
sort = {{ sort }}
order = {{ order }}
bonus = {{ bonuses }}
includeTags = {{ includeTags }}
excludeTags = {{ excludeTags }}
user = {{ user }}
displayUser = {{ displayUser }}

filter = {{ songFilter }}
      </pre
        >
      </BCard>
    </div>
  </PageFrame>
</template>
