<script setup lang="ts">
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { type CheckboxOption, type CheckboxValue } from "bootstrap-vue-next";
import { optionsFromText, valuesFromOptions, textFromValues } from "@/models/CheckboxTypes";
import { computed, ref } from "vue";
import { DanceFilter } from "@/models/DanceDatabase/DanceFilter";
import { Meter } from "@/models/DanceDatabase/Meter";

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
const groups = fullDB.groups.map((g) => g.name).filter((n) => n !== "Performance");
const danceDatabase = fullDB.filter(new DanceFilter({ groups: groups }));

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
  const filter = new DanceFilter({
    styles: textFromValues(styles.value, styleOptions.value),
    groups: textFromValues(types.value, typeOptions.value),
    meters: meters.value,
    organizations: textFromValues(organizations.value, organizationOptions.value),
  });
  return danceDatabase.filter(filter).dances;
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
      ></CheckedList>
      <CheckedList v-model="types" class="col-md" type="Type" :options="typeOptions"></CheckedList>
      <CheckedList
        v-model="meters as unknown as CheckboxValue[]"
        class="col-md"
        type="Meter"
        :options="meterOptions"
      ></CheckedList>
      <CheckedList
        v-model="organizations"
        class="col-md"
        type="Organization"
        :options="organizationOptions"
      ></CheckedList>
    </div>
    <div class="row">
      <TempoList class="col-md" :dances="dances"></TempoList>
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
