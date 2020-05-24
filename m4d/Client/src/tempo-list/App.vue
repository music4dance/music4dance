<template>
  <div id="app" class='container-fluid'>
    <h1 style="text-align:center">Partner Dancing Tempo List</h1>
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
        :styles="styles"
        :types="types"
        :meters="meters"
        :organizations="organizations"
        :allStyles="allStyles"
        :allTypes="allTypes"
        :allMeters="allMeters"
      ></dance-list>
    </div>
  </div>
</template>

<script lang="ts">
import { Component, Vue } from 'vue-property-decorator';
import CheckedList from './components/CheckedList.vue';
import DanceList from './components/DanceList.vue';
import { ListOption, valuesFromOptions, optionsFromText } from '../model/ListOption';
import { getStyles, getTypes } from '../model/DanceManager';

@Component({
  components: {
    CheckedList,
    DanceList,
  },
})
export default class App extends Vue {
    private styleOptions: ListOption[];
    private styles: string[];
    private allStyles: string[];

    private typeOptions: ListOption[];
    private types: string[];
    private allTypes: string[];

    private meterOptions: ListOption[];
    private meters: string[];
    private allMeters: string[];

    private organizationOptions: ListOption[];
    private organizations: string[];
    private allOrganizations: string[];

    constructor() {
      super();
      this.styleOptions = optionsFromText(this.filterUnused(getStyles()));
      this.allStyles = valuesFromOptions(this.styleOptions);
      const styles = (window as any).initialStyles;
      this.styles = styles ? styles : this.allStyles;

      this.typeOptions  = optionsFromText(this.filterUnused(getTypes()));
      this.allTypes = valuesFromOptions(this.typeOptions);
      const types = (window as any).initialTypes;
      this.types = types ? types : this.allTypes;

      this.meterOptions = [
        { text: '2/4 MPM', value: '2-4' },
        { text: '3/4 MPM', value: '3-4' },
        { text: '4/4 MPM', value: '4-4' },
      ];
      this.allMeters = valuesFromOptions(this.meterOptions);
      const meters = (window as any).initialMeters;
      this.meters = meters ? meters : this.allMeters;

      this.organizationOptions = [
        { text: 'DanceSport', value: 'dancesport' },
        { text: 'NDCA (Silver/Gold or Professional/Amateur)', value: 'ndca-1' },
        { text: 'NDCA (Bronze or ProAm)', value: 'ndca-2' },
      ];
      this.allOrganizations = valuesFromOptions(this.organizationOptions);
      const organizations = (window as any).initialOrganizations;
      this.organizations = organizations ? organizations : this.allOrganizations;
    }

  private filterUnused(list: string[]): string[] {
      return list.filter((s) => s !== 'Performance');
    }
}
</script>

<style lang="scss">
#app {
  font-family: 'Avenir', Helvetica, Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
}
</style>
