<template>
  <b-modal
    id="danceChooser"
    header-bg-variant="primary"
    header-text-variant="light"
    hide-footer
  >
    <template v-slot:modal-title>
      <b-icon-award></b-icon-award>&nbsp;Choose Dance Style
    </template>
    <b-button
      block
      v-if="danceId"
      variant="outline-primary"
      @click="choose()"
      style="margin-bottom: 0.5em"
    >
      Search All Dance Styles
    </b-button>
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
    <b-tabs>
      <b-tab title="By Name" :active="!hasTempo">
        <b-list-group>
          <b-list-group-item
            v-for="dance in sortedDances"
            :key="dance.id"
            button
            :active="danceId === dance.id"
            :disabled="exists(dance.id)"
            @click="chooseEvent(dance.id, $event)"
          >
            <dance-name
              :dance="dance"
              :showSynonyms="true"
              :showTempo="tempoType"
              :hideLink="hideNameLink"
            ></dance-name>
          </b-list-group-item>
        </b-list-group>
      </b-tab>
      <b-tab title="By Style">
        <b-list-group>
          <b-list-group-item
            v-for="(dance, idx) in groupedDances"
            :key="idx"
            button
            :variant="groupVariant(dance)"
            :class="{ item: dance.isGroup, 'sub-item': !dance.isGroup }"
            :active="danceId === dance.id"
            :disabled="exists(dance.id) || (dance.isGroup && !includeGroups)"
            @click="chooseEvent(dance.id, $event)"
          >
            <dance-name
              :dance="dance"
              :showSynonyms="true"
              :showTempo="tempoType"
              :hideLink="hideNameLink"
            ></dance-name>
          </b-list-group-item>
        </b-list-group>
      </b-tab>
      <b-tab title="By Tempo" active v-if="hasTempo">
        <dance-list
          :dances="tempoFiltered"
          :beatsPerMinute="tempo"
          :beatsPerMeasure="numerator"
          :epsilonPercent="20"
          :hideNameLink="true"
          @choose-dance="choose"
        ></dance-list>
      </b-tab>
    </b-tabs>
  </b-modal>
</template>

<script lang="ts">
import DanceName from "@/components/DanceName.vue";
import AdminTools from "@/mix-ins/AdminTools";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import { DanceStats } from "@/model/DanceStats";
import { TempoType } from "@/model/TempoType";
import { TypeStats } from "@/model/TypeStats";
import DanceList from "@/pages/tempo-counter/components/DanceList.vue";
import "reflect-metadata";
import { PropType } from "vue";
import mixins from "vue-typed-mixins";

export default mixins(AdminTools, EnvironmentManager).extend({
  components: { DanceName, DanceList },
  props: {
    danceId: String,
    filterIds: Array as PropType<string[]>,
    tempo: Number,
    numerator: Number,
    includeGroups: Boolean,
    hideNameLink: Boolean,
  },
  data() {
    return new (class {
      nameFilter = "";
    })();
  },
  computed: {
    sortedDances(): DanceStats[] {
      const environment = this.environment;
      const includeGroups = this.includeGroups;

      return environment
        ? this.filterAll(environment.flatStats)
            .filter((d) => includeGroups || !d.isGroup)
            .sort((a, b) => a.name.localeCompare(b.name))
        : [];
    },
    groupedDances(): DanceStats[] {
      const environment = this.environment;
      return environment ? this.filterAll(environment.groupedStats, true) : [];
    },
    dances(): DanceStats[] {
      const environment = this.environment;
      return environment && environment.tree ? environment.tree : [];
    },
    tempoFiltered(): TypeStats[] {
      return DanceEnvironment.filterByName(
        this.danceTypes,
        this.nameFilter,
        false,
        this.isAdmin
      ) as TypeStats[];
    },
    danceTypes(): TypeStats[] {
      const environment = this.environment;
      return environment && environment.dances ? environment.dances : [];
    },
    tempoType(): TempoType {
      return TempoType.Measures;
    },
    hasTempo(): boolean {
      return !!this.tempo && !!this.numerator;
    },
  },
  methods: {
    exists(danceId: string): boolean {
      const filtered = this.filterIds;
      if (!filtered) {
        return false;
      }
      return !!filtered.find((id) => id === danceId);
    },
    chooseEvent(id?: string, event?: MouseEvent): void {
      this.choose(id, event?.ctrlKey);
    },
    choose(id?: string, persist?: boolean): void {
      this.$emit("choose-dance", id, persist);
    },
    groupVariant(dance: DanceStats): string | undefined {
      return dance.isGroup && !(this.danceId === dance.id)
        ? "primary"
        : undefined;
    },
    filterAll(dances: DanceStats[], includeChildren = false): DanceStats[] {
      return DanceEnvironment.filterByName(
        dances,
        this.nameFilter,
        includeChildren,
        this.isAdmin
      );
    },
  },
});
</script>

<style lang="scss" scoped>
.sub-item {
  padding-left: 2em;
}
.item {
  font-weight: bolder;
}
</style>
