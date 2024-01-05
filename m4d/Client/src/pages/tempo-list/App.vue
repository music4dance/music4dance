<template>
  <page id="app">
    <div class="row">
      <checked-list
        class="col-md"
        type="Style"
        :options="styleOptions"
        v-model="styles"
      ></checked-list>
      <checked-list
        class="col-md"
        type="Type"
        :options="typeOptions"
        v-model="types"
      ></checked-list>
      <checked-list
        class="col-md"
        type="Meter"
        :options="meterOptions"
        v-model="meters"
      ></checked-list>
      <checked-list
        class="col-md"
        type="Organization"
        :options="organizationOptions"
        v-model="organizations"
      ></checked-list>
    </div>
    <div class="row">
      <dance-list
        class="col-md"
        :dances="dances"
        :styles="styles"
        :types="types"
        :meters="meters"
        :organizations="organizations"
        :allStyles="allStyles"
        :allTypes="allTypes"
        :allMeters="allMeters"
      ></dance-list>
    </div>
    <div class="row">
      <div class="col">
        <p>
          Dancesport Tempi are pulled from the WDSF rulles:
          <a
            href="http://www.worlddancesport.org/Rule/Athlete/Competition"
            target="_blank"
          >
            http://www.worlddancesport.org/Rule/Athlete/Competition</a
          >
          and the USADance rules:
          <a
            href="https://usadance.org/general/custom.asp?page=Rules"
            target="_blank"
            >https://usadance.org/general/custom.asp?page=Rules</a
          >
        </p>
        <p>
          NDCA (National Dance Council of America) Tempi are pulled from here:
          <a
            href="https://www.ndca.org/pages/ndca_rule_book/Default.asp"
            target="_blank"
            >http://ndca.org/pages/ndca_rule_book/</a
          >
        </p>
        <p>Some other sites with useful tempo information:</p>
        <ul>
          <li>
            <a href="http://www.superdancing.com/tempo.asp" target="_blank"
              >www.superdancing.com</a
            >
          </li>
          <li>
            <a
              href="https://www.hollywoodballroomdc.com/recommended-tempos-for-dance-music/"
              target="_blank"
              >www.hollywoodballroomdc.com</a
            >
          </li>
          <li>
            <a
              href="https://www.centralhome.com/ballroomcountry/temp.htm"
              target="_blank"
              >www.centralhome.com</a
            >
          </li>
        </ul>
        <p>
          If there are other organizations that you would like to see included
          in this list, please
          <a href="https://music4dance.blog/feedback/">contact us</a> and we'll
          be happy to take a look.
        </p>
      </div>
    </div>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import { safeEnvironment } from "@/helpers/DanceEnvironmentManager";
import { DanceType } from "@/model/DanceType";
import {
  ListOption,
  optionsFromText,
  valuesFromOptions,
} from "@/model/ListOption";
import Vue from "vue";
import CheckedList from "./components/CheckedList.vue";
import DanceList from "./components/DanceList.vue";

interface TempoListModel {
  styles?: string[];
  types?: string[];
  meters?: string[];
  organizations?: string[];
}

declare const model: TempoListModel;

export default Vue.extend({
  components: {
    CheckedList,
    DanceList,
    Page,
  },
  props: {},
  data() {
    const meterOptions = [
      { text: "2/4 MPM", value: "2-4" },
      { text: "3/4 MPM", value: "3-4" },
      { text: "4/4 MPM", value: "4-4" },
    ];
    const allMeters = valuesFromOptions(meterOptions);
    const organizationOptions = [
      { text: "DanceSport", value: "dancesport" },
      { text: "NDCA", value: "ndca" },
    ];
    const allOrganizations = valuesFromOptions(organizationOptions);
    return new (class {
      dances: DanceType[] = [];
      styleOptions: ListOption[] = [];
      styles: string[] = [];
      allStyles: string[] = [];

      typeOptions: ListOption[] = [];
      types: string[] = [];
      allTypes: string[] = [];

      meterOptions: ListOption[] = meterOptions;
      meters: string[] = filterValid(allMeters, model.meters);
      allMeters: string[] = allMeters;

      organizationOptions: ListOption[] = organizationOptions;
      organizations: string[] = filterValid(
        allOrganizations,
        model.organizations
      );
      allOrganizations: string[] = allOrganizations;
    })();
  },
  computed: {},
  methods: {},
  created(): void {
    const environment = safeEnvironment();
    this.dances = environment.flatTypes.filter((dt) => !dt.inGroup("PRF"));
    this.styleOptions = optionsFromText(filterUnused(environment.styles));
    this.allStyles = valuesFromOptions(this.styleOptions);
    this.styles = filterValid(this.allStyles, model.styles);

    this.typeOptions = optionsFromText(filterUnused(environment.types));
    this.allTypes = valuesFromOptions(this.typeOptions);
    this.types = filterValid(this.allTypes, model.types);
  },
});

function filterUnused(list: string[]): string[] {
  return list.filter((s) => s !== "Performance");
}
function filterValid(all: string[], selected?: string[]): string[] {
  return selected ? selected.filter((s) => all.find((a) => a === s)) : all;
}
</script>
