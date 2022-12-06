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
      :showTempo="showTempo"
      :showSynonyms="true"
    ></dance-list>
  </div>
</template>

<script lang="ts">
import DanceList from "@/components/DanceList.vue";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import { DanceStats } from "@/model/DanceStats";
import { TempoType } from "@/model/TempoType";
import Vue, { PropType } from "vue";

export default Vue.extend({
  components: { DanceList },
  props: {
    dances: { type: [] as PropType<DanceStats[]>, required: true },
  },
  data() {
    return new (class {
      nameFilter = "";
      showTempo = TempoType.Both;
    })();
  },
  computed: {
    filteredDances(): DanceStats[] {
      return DanceEnvironment.filterByName(this.dances, this.nameFilter, true);
    },
  },
});
</script>
