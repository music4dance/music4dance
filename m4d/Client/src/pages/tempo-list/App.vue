<template>
  <page
    id="app"
    :consumesEnvironment="true"
    @environment-loaded="onEnvironmentLoaded"
  >
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
        type="Oranization"
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
          <a href="http://www.worlddancesport.org/Rule/Athlete/Competition">
            http://www.worlddancesport.org/Rule/Athlete/Competition</a
          >
          and the USADance rules:
          <a href="https://usadance.org/general/custom.asp?page=Rules"
            >https://usadance.org/general/custom.asp?page=Rules</a
          >
        </p>
        <p>
          NDCA (National Dance Council of America) Tempi are pulled from here:
          <a href="https://www.ndca.org/pages/ndca_rule_book/Default.asp"
            >http://ndca.org/pages/ndca_rule_book/</a
          >
        </p>
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
import { Component, Vue } from "vue-property-decorator";
import Page from "@/components/Page.vue";
import CheckedList from "./components/CheckedList.vue";
import DanceList from "./components/DanceList.vue";
import {
  ListOption,
  valuesFromOptions,
  optionsFromText,
} from "@/model/ListOption";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { DanceType } from "@/model/DanceType";

declare global {
  interface Window {
    initialStyles?: string[];
    initialTypes?: string[];
    initialMeters?: string[];
    initialOrganizations?: string[];
  }
}

@Component({
  components: {
    CheckedList,
    DanceList,
    Page,
  },
})
export default class App extends Vue {
  private dances: DanceType[] = [];
  private styleOptions: ListOption[] = [];
  private styles: string[] = [];
  private allStyles: string[] = [];

  private typeOptions: ListOption[] = [];
  private types: string[] = [];
  private allTypes: string[] = [];

  private meterOptions: ListOption[];
  private meters: string[];
  private allMeters: string[];

  private organizationOptions: ListOption[];
  private organizations: string[];
  private allOrganizations: string[];

  constructor() {
    super();
    this.meterOptions = [
      { text: "2/4 MPM", value: "2-4" },
      { text: "3/4 MPM", value: "3-4" },
      { text: "4/4 MPM", value: "4-4" },
    ];
    this.allMeters = valuesFromOptions(this.meterOptions);
    this.meters = this.filterValid(this.allMeters, window.initialMeters);

    this.organizationOptions = [
      { text: "DanceSport", value: "dancesport" },
      { text: "NDCA (Silver/Gold or Professional/Amateur)", value: "ndca-1" },
      { text: "NDCA (Bronze or ProAm)", value: "ndca-2" },
    ];
    this.allOrganizations = valuesFromOptions(this.organizationOptions);
    this.organizations = this.filterValid(
      this.allOrganizations,
      window.initialOrganizations
    );
    const organizations = window.initialOrganizations;
    this.organizations = organizations ? organizations : this.allOrganizations;
  }

  private onEnvironmentLoaded(environment: DanceEnvironment): void {
    this.dances = environment.flatTypes.filter((dt) => !dt.inGroup("PRF"));
    this.styleOptions = optionsFromText(this.filterUnused(environment.styles));
    this.allStyles = valuesFromOptions(this.styleOptions);
    this.styles = this.filterValid(this.allStyles, window.initialStyles);

    this.typeOptions = optionsFromText(this.filterUnused(environment.types));
    this.allTypes = valuesFromOptions(this.typeOptions);
    this.types = this.filterValid(this.allTypes, window.initialTypes);
  }

  private filterUnused(list: string[]): string[] {
    return list.filter((s) => s !== "Performance");
  }

  private filterValid(all: string[], selected?: string[]): string[] {
    return selected ? selected.filter((s) => all.find((a) => a === s)) : all;
  }
}
</script>
