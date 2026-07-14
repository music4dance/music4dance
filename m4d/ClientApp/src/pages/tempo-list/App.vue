<script setup lang="ts">
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { type CheckboxOption, type CheckboxValue } from "bootstrap-vue-next";
import { optionsFromText, valuesFromOptions, textFromValues } from "@/models/CheckboxTypes";
import { computed, ref } from "vue";
import { DanceDatabase } from "@/models/DanceDatabase/DanceDatabase";
import { DanceFilter } from "@/models/DanceDatabase/DanceFilter";
import { Meter } from "@/models/DanceDatabase/Meter";
import type { DanceType } from "@/models/DanceDatabase/DanceType";

// TODO: Clean up the CheckboxOptions structures
// Consider disabling checkboxes that don't make sense for the current selection
interface TempoListModel {
  styles?: string[];
  types?: string[];
  meters?: string[];
  organizations?: string[];
}

declare const model_: TempoListModel;
const model = model_ || {};

const fullDB = safeDanceDatabase();
// Performance dances (and the occasional non-Performance-group dance like Pattern) have no real
// tempo - their instances carry a placeholder range rather than an actual BPM/MPM band - so they
// can never usefully appear on this page. Filtering by tempoRange.isInfinite (rather than by
// group name) catches all of them, not just the ones grouped under "Performance".
const timedDances = fullDB.dances.filter((d) => !d.tempoRange.isInfinite);
const danceDatabase = new DanceDatabase({ dances: timedDances, groups: fullDB.groups }).filter(
  new DanceFilter({}),
);

const { options: styleOptions, values: styles } = buildList(danceDatabase.styles, model.styles);

const { options: typeOptions, values: types } = buildList(
  danceDatabase.groups.map((g) => g.name),
  model.types,
);

// INT-TODO: I think CheckboxValue should include unknown and not require a cast below
const meterOptions: CheckboxOption[] = [
  { text: "2/4", value: new Meter(2, 4) as unknown as CheckboxValue },
  { text: "3/4", value: new Meter(3, 4) as unknown as CheckboxValue },
  { text: "4/4", value: new Meter(4, 4) as unknown as CheckboxValue },
];
const meterValues = valuesFromOptions(meterOptions) as unknown[] as Meter[];
const meters = ref<Meter[]>(meterValues);

const { options: organizationOptions, values: organizations } = buildList(
  danceDatabase.organizations,
  model.organizations,
);

const nameFilter = ref("");

function buildList(values: string[], current?: string[]) {
  const options = optionsFromText(values);
  const all = valuesFromOptions(options);
  return {
    options: ref<CheckboxOption[]>(options),
    values: ref<string[]>(filterValid(all as string[], current)),
  };
}

function filterValid(all: string[], selected?: string[]): string[] {
  return selected ? selected.filter((s) => all.find((a) => a === s)) : all;
}

const dances = computed(() => {
  const selectedOrganizations = textFromValues(organizations.value, organizationOptions.value);
  const filter = new DanceFilter({
    styles: textFromValues(styles.value, styleOptions.value),
    groups: textFromValues(types.value, typeOptions.value),
    meters: meters.value,
    // "Every organization selected" must mean "no organization restriction" (like DanceFilter's
    // undefined), not "match only dances affiliated with one of these organizations" - the
    // latter incorrectly excludes dances with no organization affiliation at all (e.g. Social
    // dances like Lindy Hop), since DanceFilter.matchOrganizations can never match an empty list
    // against anything.
    organizations:
      selectedOrganizations.length === organizationOptions.value.length
        ? undefined
        : selectedOrganizations,
  });
  return DanceDatabase.filterByName(
    danceDatabase.filter(filter).dances,
    nameFilter.value,
  ) as DanceType[];
});

// Exposed for testing
defineExpose({
  styles,
  styleOptions,
  types,
  typeOptions,
  meters,
  organizations,
  organizationOptions,
  nameFilter,
  dances,
});
</script>

<template>
  <PageFrame id="app">
    <div class="row">
      <CheckedList v-model="styles" class="col-md" type="Style" :options="styleOptions" />
      <CheckedList v-model="types" class="col-md" type="Type" :options="typeOptions" />
      <CheckedList
        v-model="meters as unknown as CheckboxValue[]"
        class="col-md"
        type="Meter"
        :options="meterOptions"
      />
      <CheckedList
        v-model="organizations"
        class="col-md"
        type="Organization"
        :options="organizationOptions"
      />
    </div>
    <div class="row">
      <NameFilterInput
        id="tempo-name-filter"
        v-model="nameFilter"
        class="col-md"
        placeholder="Filter Dances"
      />
    </div>
    <div class="row">
      <TempoList class="col-md" :dances="dances" />
    </div>
    <div class="row">
      <div class="col">
        <p>
          Dancesport Tempi are pulled from the WDSF rulles:
          <a href="http://www.worlddancesport.org/Rule/Athlete/Competition" target="_blank">
            http://www.worlddancesport.org/Rule/Athlete/Competition</a
          >
          and the USADance rules:
          <a href="https://usadance.org/general/custom.asp?page=Rules" target="_blank"
            >https://usadance.org/general/custom.asp?page=Rules</a
          >
        </p>
        <p>
          NDCA (National Dance Council of America) Tempi are pulled from here:
          <a href="https://www.ndca.org/pages/ndca_rule_book/Default.asp" target="_blank"
            >http://ndca.org/pages/ndca_rule_book/</a
          >
        </p>
        <p>Some other sites with useful tempo information:</p>
        <ul>
          <li>
            <a href="http://www.superdancing.com/tempo.asp" target="_blank">www.superdancing.com</a>
          </li>
          <li>
            <a
              href="https://www.hollywoodballroomdc.com/recommended-tempos-for-dance-music/"
              target="_blank"
              >www.hollywoodballroomdc.com</a
            >
          </li>
          <li>
            <a href="https://www.centralhome.com/ballroomcountry/temp.htm" target="_blank"
              >www.centralhome.com</a
            >
          </li>
        </ul>
        <p>
          If there are other organizations that you would like to see included in this list, please
          <a href="https://music4dance.blog/feedback/">contact us</a> and we'll be happy to take a
          look.
        </p>
      </div>
    </div>
  </PageFrame>
</template>
