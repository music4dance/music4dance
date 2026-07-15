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
const meters = ref<Meter[]>(filterValidMeters(meterOptions, model.meters));

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

// Mirrors filterValid()'s "silently drop anything that isn't a valid option" behavior, but for
// the hard-coded Meter options: matches server-provided strings (e.g. "3/4") against each
// option's text, then returns the corresponding Meter instances rather than strings.
function filterValidMeters(options: CheckboxOption[], selected?: string[]): Meter[] {
  const matching = selected ? options.filter((o) => selected.includes(o.text)) : options;
  return matching.map((o) => o.value as unknown as Meter);
}

const selectedStyles = computed(() => textFromValues(styles.value, styleOptions.value));
const selectedTypes = computed(() => textFromValues(types.value, typeOptions.value));
const selectedOrganizations = computed(() =>
  textFromValues(organizations.value, organizationOptions.value),
);
// "Every organization selected" must mean "no organization restriction" (like DanceFilter's
// undefined), not "match only dances affiliated with one of these organizations" - the latter
// incorrectly excludes dances with no organization affiliation at all (e.g. Social dances like
// Lindy Hop), since DanceFilter.matchOrganizations can never match an empty list against anything.
const normalizedOrganizations = computed(() =>
  selectedOrganizations.value.length === organizationOptions.value.length
    ? undefined
    : selectedOrganizations.value,
);

const dances = computed(() => {
  const filter = new DanceFilter({
    styles: selectedStyles.value,
    groups: selectedTypes.value,
    meters: meters.value,
    organizations: normalizedOrganizations.value,
  });
  return DanceDatabase.filterByName(
    danceDatabase.filter(filter).dances,
    nameFilter.value,
  ) as DanceType[];
});

// Result counts per dropdown, used to annotate/gray out options that can't produce any results
// given the *other three* filters' current selection - each option is counted independently of
// what else is checked in its own dropdown (unlike `dances`, which ANDs every dropdown's full
// selection together). Deliberately not memoized beyond the per-dropdown computed: re-filtering
// a few dozen times per keystroke is cheap at this dataset's size.
function countMatching(filter: DanceFilter): number {
  return DanceDatabase.filterByName(danceDatabase.filter(filter).dances, nameFilter.value).length;
}

// Options' `.value` is a kebab id (e.g. "international-standard"); DanceFilter.styles/groups/
// organizations compare against the original display text (e.g. "International Standard") - the
// same text that textFromValues() above already resolves for the *selected* arrays. Meter is the
// exception: its options were never kebab-encoded, so `.value` is already the real Meter to match.
const styleCounts = computed(() =>
  styleOptions.value.map(({ text }) =>
    countMatching(
      new DanceFilter({
        styles: [text],
        groups: selectedTypes.value,
        meters: meters.value,
        organizations: normalizedOrganizations.value,
      }),
    ),
  ),
);

const typeCounts = computed(() =>
  typeOptions.value.map(({ text }) =>
    countMatching(
      new DanceFilter({
        styles: selectedStyles.value,
        groups: [text],
        meters: meters.value,
        organizations: normalizedOrganizations.value,
      }),
    ),
  ),
);

const meterCounts = computed(() =>
  meterOptions.map(({ value }) =>
    countMatching(
      new DanceFilter({
        styles: selectedStyles.value,
        groups: selectedTypes.value,
        meters: [value as unknown as Meter],
        organizations: normalizedOrganizations.value,
      }),
    ),
  ),
);

const organizationCounts = computed(() =>
  organizationOptions.value.map(({ text }) =>
    countMatching(
      new DanceFilter({
        styles: selectedStyles.value,
        groups: selectedTypes.value,
        meters: meters.value,
        // Deliberately the strict single-organization count, not the "all selected" undefined
        // normalization above - this answers "how many dances are affiliated with just this
        // organization," which is what the count next to its checkbox should mean.
        organizations: [text],
      }),
    ),
  ),
);

// Exposed for testing
defineExpose({
  styles,
  styleOptions,
  styleCounts,
  types,
  typeOptions,
  typeCounts,
  meters,
  meterCounts,
  organizations,
  organizationOptions,
  organizationCounts,
  nameFilter,
  dances,
});
</script>

<template>
  <PageFrame id="app">
    <div class="row">
      <CheckedList
        v-model="styles"
        class="col-md"
        type="Style"
        :options="styleOptions"
        :counts="styleCounts"
      />
      <CheckedList
        v-model="types"
        class="col-md"
        type="Type"
        :options="typeOptions"
        :counts="typeCounts"
      />
      <CheckedList
        v-model="meters as unknown as CheckboxValue[]"
        class="col-md"
        type="Meter"
        :options="meterOptions"
        :counts="meterCounts"
      />
      <CheckedList
        v-model="organizations"
        class="col-md"
        type="Organization"
        :options="organizationOptions"
        :counts="organizationCounts"
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
