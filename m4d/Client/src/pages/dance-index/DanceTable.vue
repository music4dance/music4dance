<template>
  <div>
    <b-input-group class="mb-2">
      <b-form-input
        type="text"
        v-model="nameFilter"
        placeholder="Filter Dances"
        autofocus
      ></b-form-input>
      <b-input-group-append is-text
        ><b-icon-search></b-icon-search
      ></b-input-group-append>
    </b-input-group>
    <dance-list
      :dances="filteredDances"
      :flush="false"
      :showTempo="true"
    ></dance-list>
  </div>
</template>

<script lang="ts">
import DanceList from "@/components/DanceList.vue";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { DanceStats } from "@/model/DanceStats";
import { Component, Prop, Vue } from "vue-property-decorator";

@Component({
  components: {
    DanceList,
  },
})
export default class DanceTable extends Vue {
  @Prop() private dances!: DanceStats[];
  private nameFilter = "";

  private get filteredDances(): DanceStats[] {
    return DanceEnvironment.filterByName(this.dances, this.nameFilter, true);
  }
}
</script>
