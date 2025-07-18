<script setup lang="ts">
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
import { DanceThreshold } from "@/models/DanceThreshold";

const context = getMenuContext();
const danceDB: DanceDatabase = safeDanceDatabase();

const allDances = danceDB.all;

const tagDatabase = safeTagDatabase();
const filter = getQueryFilter();
const danceQueryInit = new DanceQuery(filter.dances);

const showDiagnostics = getShowDiagnostics();
const keyWords = ref(filter.searchString ?? "");
const advancedText = ref(false);
const danceThresholds = ref(danceQueryInit.danceThresholds);
const danceConnector = ref(danceQueryInit.isExclusive ? "all" : "any");
const dances = computed<string[]>({
  get: () => danceThresholds.value.map((d) => d.id),
  set: (value: string[]): void => {
    danceThresholds.value = value.map((id) => {
      const existing = danceThresholds.value.find((d) => d.id === id);
      return existing ? existing : new DanceThreshold({ id: id, threshold: 1 });
    });
  },
});
const hasThresholds = computed(() => {
  return danceThresholds.value.some((d) => d.threshold > 1);
});
const showThresholds = ref(hasThresholds.value);

const tags: Tag[] = tagDatabase.tags;
const includeTags = ref(filter.tags ? extractTags(filter.tags, true) : []);
const excludeTags = ref(filter.tags ? extractTags(filter.tags, false) : []);

const tempoMin = ref(filter.tempoMin ?? 0);
const tempoMax = ref(filter.tempoMax ?? 400);

const lengthMin = ref(filter.lengthMin ?? 0);
const lengthMax = ref(filter.lengthMax ?? 600);

const userQueryInit = new UserQuery(filter.user);

const user = ref(userQueryInit.userName ?? context.userName ?? "");
const displayUser = ref(userQueryInit.displayName);

const bonuses = ref(computeBonuses());
const validated = ref(false);
const services = ref(filter.purchase ? filter.purchase.trim().split("") : []);
const activity = ref(userQueryInit.parts);

const computedDefault = (): string => {
  const value = !!keyWords.value ? "Closest Match" : "Dance Rating";
  return `Default (${value})`;
};

const sortOptions = computed(() => [
  { text: computedDefault(), value: null },
  { text: "Dance Rating", value: SortOrder.Dances },
  { text: "Closest Match", value: SortOrder.Match },
  { text: "Title", value: SortOrder.Title },
  { text: "Artist", value: SortOrder.Artist },
  { text: "Tempo", value: SortOrder.Tempo },
  { text: "Length", value: SortOrder.Length },
  { text: "Last Modified", value: SortOrder.Modified },
  { text: "Last Edited", value: SortOrder.Edited },
  { text: "When Added", value: SortOrder.Created },
  { text: "Energy", value: SortOrder.Energy },
  { text: "Mood", value: SortOrder.Mood },
  { text: "Strength of Beat", value: SortOrder.Beat },
  { text: "Comments", value: SortOrder.Comments },
]);

const sortInit = new SongSort(filter.sortOrder, filter.TextSearch);
const sortId = ref(sortInit.id || null);
const sortDirection = ref(sortInit.direction);

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
  const showDiagnostics = params.get("showDiagnostics") ?? params.get("showdiagnostics");

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
  const danceQuery = DanceQuery.fromParts(
    danceThresholds.value.map((t) => t.toString()),
    danceConnector.value === "all",
  );
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

  filter.action = "advanced";
  filter.searchString = keyWords.value;
  filter.dances = danceQuery.query;
  filter.sortOrder = SongSort.fromParts(sortId.value ?? undefined, sortDirection.value).query;
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
      tempoMax.value = min;
      tempoMin.value = max;
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
  sortId.value = null;
  sortDirection.value = "asc";
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
          <div style="border: 1px solid #ced4da; border-radius: 0.25rem">
            <DanceSelector id="dance-selector" v-model="dances" :dance-list="allDances" />
            <div class="d-flex mb-2">
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
              <BFormCheckbox
                id="show-thresholds"
                v-model="showThresholds"
                :disabled="!dances.length || hasThresholds"
                switch
              >
                Show Thresholds
              </BFormCheckbox>
            </div>
            <div v-if="showThresholds" class="mx-3 mb-2">
              <div v-for="threshold in danceThresholds" :key="threshold.id" class="mt-2">
                <BFormSpinbutton
                  id="`sb-${dance}`"
                  v-model="threshold.threshold"
                  inline
                  min="1"
                  max="30"
                  size="sm"
                />
                <label :for="`sb-${threshold.dance.name}`" class="ms-2">{{
                  threshold.dance.name
                }}</label>
              </div>
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
          />
        </BFormGroup>

        <BFormGroup id="exclude-tags-group" label="Exclude Tags:">
          <TagCategorySelector
            id="exclude-tags"
            v-model="excludeTags"
            :tag-list="tags"
            choose-label="Choose Tags to Exclude"
            search-label="Search Tags"
            empty-label="No more tags to choose"
          />
        </BFormGroup>

        <BFormGroup id="tempo-range-group" label="Tempo range (BPM):" label-for="tempo-range">
          <BFormGroup id="tempo-range">
            <div class="d-flex">
              <BFormInput
                id="tempo-min"
                v-model.number="tempoMin"
                type="number"
                min="0"
                max="400"
                style="width: 6rem"
              />
              <span class="mx-2 pt-2">to</span>
              <BFormInput
                id="tempo-max"
                v-model.number="tempoMax"
                type="number"
                min="0"
                max="400"
                style="width: 6rem"
              />
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
                v-model.number="lengthMin"
                type="number"
                min="0"
                max="600"
                style="width: 6rem"
              />
              <span class="mx-2 pt-2">to</span>
              <BFormInput
                id="length-max"
                v-model.number="lengthMax"
                type="number"
                min="0"
                max="600"
                style="width: 6rem"
              />
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
            />
            <BFormSelect id="activity" v-model="computedActivity" :options="activities" />
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
          <BFormSelect id="sort" v-model="sortId" :options="sortOptions" required />
          <BFormRadioGroup id="sort-order" v-model="sortDirection" name="sort-order" class="my-2">
            <BFormRadio value="asc"
              >Ascending (A-Z, Slow-Fast, Newest-Oldest, Shortest-Longest)</BFormRadio
            >
            <BFormRadio value="desc"
              >Descending (Z-A, Fast-Slow, Oldest-Newest, Longest-Shortest)</BFormRadio
            >
          </BFormRadioGroup>
        </BFormGroup>

        <BAlert :model-value="true" variant="success">
          {{ songFilter.description }}
        </BAlert>

        <div class="d-flex justify-content-between w-100 mx-1 mb-2">
          <BButton type="reset" variant="secondary">Reset</BButton>
          <BButton type="submit" variant="primary">Submit</BButton>
        </div>
      </BForm>
      <BCard v-if="showDiagnostics" class="mt-3" header="Form Data Result">
        <pre class="m-0">
searchString = {{ keyWords }}
dances = {{ dances }}
danceThresholds = {{ danceThresholds }}
danceConnector = {{ danceConnector }}
tempoMin = {{ tempoMin }}
tempoMax = {{ tempoMax }}
lengthMin = {{ lengthMin }}
lengthMax = {{ lengthMax }}
activity = {{ computedActivity }}
services = {{ services }}
sort = {{ sortId }}
order = {{ sortDirection }}
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
